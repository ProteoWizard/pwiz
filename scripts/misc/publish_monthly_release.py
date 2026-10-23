#!/usr/bin/env python3
#
# Pins ProteoWizard's monthly releases and publishes the monthly release metadata.
#
# The monthly release of Core Windows x86_64 (bt83) is the successful default-branch build that
# finished last in its month. On the first of the next month this script pins that build, which
# exempts its installers from TeamCity's artifact cleanup, and tags it release-YYYY-MM-DD (the date
# it finished), which marks it as a release. A release can also be made between months, with
# --latest; it is permanent too, so people can depend on it, and the month still gets its regular
# release when it ends. The archive seed builds of months before 2026-10 are tagged release-YYYY-MM;
# they ran on a PR branch with a seed.commit parameter.
#
# The script then writes the metadata the download page reads, for the TeamCity config to upload
# beside releases/bt83.xml:
#
#   bt83-monthly.xml   the newest release, in the same format as bt83.xml
#   bt83-archive.json  every release, newest first:
#                      {"releases": [{"release", "version", "buildId", "commit", "artifacts"}]}
#                      where release is the tag's date, YYYY-MM-DD (or YYYY-MM for a seed), with
#                      -2, -3, ... for a later release on a day that already has one
#
# A release is any pinned, successful bt83 build on any branch with a release tag. Releasing a build
# that is already a release does nothing, so the script is safe to rerun. A build without
# installers is never released.
#
# Usage:
#   publish_monthly_release.py --output-dir DIR [--month YYYY-MM | --latest] [--dry-run]
#                              [--teamcity-user USER --teamcity-password PASSWORD | --teamcity-token TOKEN]
#
# --month defaults to the previous month (by the TeamCity server's local time). --latest releases the
# newest successful default-branch build instead. --dry-run reports what would be released without
# changing anything in TeamCity; the metadata files are still written. Without credentials the script
# reads TeamCity as a guest, which only allows --dry-run.

import argparse
import base64
import datetime
import json
import os
import re
import sys
import urllib.request

TEAMCITY_URL = "https://teamcity.labkey.org"
BUILD_TYPE = "bt83"
RELEASE_TAG = re.compile(r"^release-(\d{4}-\d{2}(?:-\d{2}(?:-\d+)?)?)$")
# The files a release publishes, as releases/bt83.xml lists them (system.artifact.paths in bt83):
# the installers and the version file, from the .NET build (2026-10 onward) or the C++ build
INSTALLER = re.compile(r"^(ProteoWizard-[\w\-.]+\.exe|pwiz-setup-[\w\-.]+\.msi)$")
RELEASE_ARTIFACT = re.compile(r"^(ProteoWizard-[\w\-.]+\.exe|installer-version\.txt|pwiz-setup-[\w\-.]+\.msi|VERSION)$")
VERSION_FILES = ["installer-version.txt", "VERSION"]
# How many recent default-branch builds to search for the last one of a month (~50 per month)
RECENT_BUILD_COUNT = 300


def main():
    parser = argparse.ArgumentParser(description="Pin bt83's monthly releases and write the monthly release metadata")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--teamcity-user")
    parser.add_argument("--teamcity-password")
    parser.add_argument("--teamcity-token", help="Bearer token, instead of --teamcity-user/--teamcity-password")
    parser.add_argument("--month", default="", help="YYYY-MM whose last build to release; defaults to the previous month")
    parser.add_argument("--latest", action="store_true", help="Release the newest successful build instead of a month's last")
    parser.add_argument("--dry-run", action="store_true", help="Report what would be released without changing TeamCity")
    args = parser.parse_args()

    if args.teamcity_token:
        teamcity = TeamCity("/app/rest", "Bearer %s" % args.teamcity_token)
    elif args.teamcity_user and args.teamcity_password:
        credentials = base64.b64encode(("%s:%s" % (args.teamcity_user, args.teamcity_password)).encode("utf-8")).decode("ascii")
        teamcity = TeamCity("/httpAuth/app/rest", "Basic %s" % credentials)
    elif args.dry_run:
        teamcity = TeamCity("/guestAuth/app/rest", None)
    else:
        parser.error("credentials are required unless --dry-run is given")
    if args.latest and args.month:
        parser.error("--month and --latest cannot be combined")

    releases = find_releases(teamcity)
    if args.latest:
        build = newest_successful_build(teamcity)
        description = "the latest successful build"
    else:
        month = args.month or previous_month(teamcity.server_time())
        if not re.match(r"^\d{4}-\d{2}$", month):
            parser.error("--month must be YYYY-MM, not '%s'" % month)
        if month in releases:
            print("%s was released by its archive seed build %s" % (month, releases[month]["buildId"]))
            build = None
        else:
            build = last_successful_build_of_month(teamcity, month)
            if build is None:
                print("##teamcity[buildProblem description='No successful %s build finished in %s']" % (BUILD_TYPE, month))
                sys.exit(1)
        description = "the last successful build of %s" % month

    if build is not None:
        release(teamcity, releases, build, description, args.dry_run)
        if not args.dry_run:
            releases = find_releases(teamcity)
    write_metadata(args.output_dir, releases, teamcity)


def release(teamcity, releases, build, description, dry_run):
    holder = next((r for r in releases.values() if r["buildId"] == build["id"]), None)
    if holder:
        print("Build %s, %s, is already release %s" % (build["id"], description, holder["release"]))
        return
    # A build whose installers were never built, or already cleaned up, would publish an empty release
    if not any(INSTALLER.match(name) for name in teamcity.artifact_names(build["id"])):
        print("##teamcity[buildProblem description='Build %s, %s, has no installers to release']" % (build["id"], description))
        sys.exit(1)
    name = unused_release_name(releases, finish_date(build))
    tag = "release-" + name
    print("%s and tag build %s (%s, finished %s), %s, as %s" % ("Dry run: would pin" if dry_run else "Pinning",
          build["id"], build.get("number"), build.get("finishDate"), description, tag))
    if not dry_run:
        teamcity.pin_and_tag(build["id"], tag, "Monthly release channel: %s" % description)


class TeamCity:
    def __init__(self, rest_root, auth):
        self._rest_url = TEAMCITY_URL + rest_root
        self._auth = auth

    def request(self, method, path, payload=None):
        # JSON bodies only: the proxy in front of teamcity.labkey.org rejects XML request bodies
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        request = urllib.request.Request(self._rest_url + path, data=data, method=method)
        if self._auth:
            request.add_header("Authorization", self._auth)
        request.add_header("Accept", "application/json")
        if data is not None:
            request.add_header("Content-Type", "application/json")
        with urllib.request.urlopen(request, timeout=60) as response:
            body = response.read().decode("utf-8")
        return json.loads(body) if body else None

    def server_time(self):
        # e.g. "20261101T060000-0700"; months follow the server's local time, as the build dates do
        return self.request("GET", "/server?fields=currentTime")["currentTime"]

    def builds(self, locator, fields):
        return self.request("GET", "/builds?locator=%s&fields=build(%s)" % (locator, fields)).get("build", [])

    def artifact_text(self, build_id, name):
        # TeamCity redirects artifact content to S3, which rejects a forwarded Authorization header,
        # so read it as a guest; the download page links to release artifacts as a guest too
        url = "%s/guestAuth/app/rest/builds/id:%s/artifacts/content/%s" % (TEAMCITY_URL, build_id, name)
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read().decode("utf-8")

    def artifact_names(self, build_id):
        files = self.request("GET", "/builds/id:%s?fields=artifacts(file(name))" % build_id).get("artifacts") or {}
        return [f["name"] for f in files.get("file", [])]

    def pin_and_tag(self, build_id, tag, comment):
        self.request("PUT", "/builds/id:%s/pinInfo" % build_id, {"status": True, "comment": {"text": comment}})
        self.request("POST", "/builds/id:%s/tags" % build_id, {"tag": [{"name": tag}]})


def previous_month(teamcity_time):
    year, month = int(teamcity_time[0:4]), int(teamcity_time[4:6])
    first_of_month = datetime.date(year, month, 1)
    return (first_of_month - datetime.timedelta(days=1)).strftime("%Y-%m")


def find_releases(teamcity):
    """Returns {release: release} for every pinned, successful bt83 build with a release tag, keyed
    by the tag's date (YYYY-MM-DD, or YYYY-MM for a seed)."""
    fields = "id,status,finishDate,defaultBranch,tags(tag(name)),revisions(revision(version)),properties(property(name,value)),artifacts(file(name))"
    # lookupLimit: by default TeamCity only searches the most recent builds, and bt83 also builds
    # every PR, so years-old releases would silently drop out of the archive
    locator = "buildType:%s,branch:default:any,pinned:true,status:SUCCESS,count:1000,lookupLimit:1000000" % BUILD_TYPE
    releases = {}
    for build in teamcity.builds(locator, fields):
        for tag in (build.get("tags") or {}).get("tag", []):
            match = RELEASE_TAG.match(tag.get("name", ""))
            if match and match.group(1) not in releases:
                releases[match.group(1)] = to_release(match.group(1), build)
    return releases


def to_release(name, build):
    artifacts = [f["name"] for f in (build.get("artifacts") or {}).get("file", []) if RELEASE_ARTIFACT.match(f["name"])]
    properties = {p["name"]: p.get("value", "") for p in (build.get("properties") or {}).get("property", [])}
    revisions = (build.get("revisions") or {}).get("revision", [])
    # A seed build ran on a PR branch and checked out its seed.commit parameter in place of that
    # branch. Guests cannot see build parameters, so a guest dry run leaves a seed's commit empty
    # rather than report the PR branch's.
    if build.get("defaultBranch", False):
        commit = revisions[0]["version"] if revisions else ""
    else:
        commit = properties.get("seed.commit", "")
    return {"release": name, "buildId": build["id"], "commit": commit, "artifacts": artifacts}


def finish_date(build):
    return parse_teamcity_time(build["finishDate"]).strftime("%Y-%m-%d")


def unused_release_name(releases, date):
    """Returns date, or date-2, date-3, ... for a later release on a day that already has one."""
    name, number = date, 1
    while name in releases:
        number += 1
        name = "%s-%d" % (date, number)
    return name


def release_order(name):
    # YYYY-MM (seed), YYYY-MM-DD and YYYY-MM-DD-N compared as numbers, so 2026-11-30-2 follows
    # 2026-11-30 and 2026-10-22 follows 2026-09
    return tuple(int(part) for part in name.split("-"))


def parse_teamcity_time(text):
    return datetime.datetime.strptime(text, "%Y%m%dT%H%M%S%z")


def recent_successful_builds(teamcity):
    locator = "buildType:%s,branch:default:true,status:SUCCESS,state:finished,count:%d" % (BUILD_TYPE, RECENT_BUILD_COUNT)
    return teamcity.builds(locator, "id,number,finishDate")


def newest_successful_build(teamcity):
    builds = recent_successful_builds(teamcity)
    return max(builds, key=lambda b: parse_teamcity_time(b["finishDate"])) if builds else None


def last_successful_build_of_month(teamcity, month):
    """Returns the successful default-branch bt83 build that finished last in month."""
    yyyymm = month.replace("-", "")
    in_month = [b for b in recent_successful_builds(teamcity) if b.get("finishDate", "").startswith(yyyymm)]
    return max(in_month, key=lambda b: parse_teamcity_time(b["finishDate"])) if in_month else None


def read_version(teamcity, release):
    # The version file is the published contract for the installer version; its name format is
    # not (see scripts/installer/build.ps1). The C++ build's VERSION separates the parts with spaces.
    for name in VERSION_FILES:
        if name in release["artifacts"]:
            return ".".join(teamcity.artifact_text(release["buildId"], name).split()) if name == "VERSION" \
                else teamcity.artifact_text(release["buildId"], name).strip()
    return ""


def write_metadata(output_dir, releases, teamcity):
    os.makedirs(output_dir, exist_ok=True)
    ordered = [dict(releases[name], version=read_version(teamcity, releases[name]))
               for name in sorted(releases, key=release_order, reverse=True)]
    with open(os.path.join(output_dir, BUILD_TYPE + "-archive.json"), "w", newline="\n") as f:
        json.dump({"releases": ordered}, f, indent=1)
    if ordered:
        latest = ordered[0]
        with open(os.path.join(output_dir, BUILD_TYPE + "-monthly.xml"), "w", newline="\n") as f:
            f.write("<artifacts>")
            for name in latest["artifacts"]:
                f.write("<artifact>/guestAuth/app/rest/builds/id:%s/artifacts/content/%s</artifact>" % (latest["buildId"], name))
            f.write("</artifacts>")
    print("Wrote %d releases to %s; latest: %s" % (len(ordered), output_dir, ordered[0]["release"] if ordered else "none"))


if __name__ == "__main__":
    main()
