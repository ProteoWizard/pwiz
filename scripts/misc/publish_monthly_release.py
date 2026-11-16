#!/usr/bin/env python3
#
# Pins ProteoWizard's monthly releases and publishes the monthly release metadata.
#
# The monthly release of Core Windows x86_64 (bt83) is the successful default-branch build that
# finished last in its month. On the first of the next month this script pins that build, which
# exempts its installers from TeamCity's artifact cleanup, and tags it as a release. A release can
# also be made at any time from the newest build, with --latest (a "daily" release); it is permanent
# too, so people can depend on it, and its month still gets its regular release when it ends.
#
# A release's tag identifies its installer: the commit date and git SHA from the installer version
# (4.0.YYDOY-sha, or 3.0.YYDOY.sha for the C++ builds), so the SHA has the installer's length:
#
#   release-YYYY-MM-DD-sha         a month's release
#   release-daily-YYYY-MM-DD-sha   a release made with --latest
#
# The archive seed builds of months before 2026-10 are month releases that ran on a PR branch with a
# seed.commit parameter.
#
# The script then writes the metadata the download page reads, for the TeamCity config to upload
# beside releases/bt83.xml:
#
#   bt83-monthly.xml   the newest release, in the same format as bt83.xml
#   bt83-archive.json  every release, newest first (a month's release after a daily one the same day):
#                      {"releases": [{"tag", "date", "daily", "version", "buildId", "commit", "artifacts"}]}
#
# A release is any pinned, successful bt83 build on any branch with a release tag; a build can hold
# both a daily and a month's tag. Releasing what is already released does nothing, so the script is
# safe to rerun. A build without installers is never released.
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
RELEASE_TAG = re.compile(r"^release-(daily-)?(\d{4}-\d{2}-\d{2})-([0-9a-f]{7,40})$")
# 4.0.YYDOY-sha (.NET build) or 3.0.YYDOY.sha (C++ build)
INSTALLER_VERSION = re.compile(r"^\d+\.\d+\.(\d{2})(\d{3})[-.]([0-9a-f]{7,40})$")
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
    parser.add_argument("--latest", action="store_true", help="Release the newest successful build (a daily release)")
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
        held = [r for r in releases if build and r["buildId"] == build["id"]]
        if held:
            print("Build %s, the latest successful build, is already released as %s" % (build["id"], held[0]["tag"]))
            build = None
        daily, description = True, "the latest successful build"
    else:
        month = args.month or previous_month(teamcity.server_time())
        if not re.match(r"^\d{4}-\d{2}$", month):
            parser.error("--month must be YYYY-MM, not '%s'" % month)
        released = [r for r in releases if not r["daily"] and r["date"].startswith(month)]
        if released:
            print("%s is already released as %s" % (month, released[0]["tag"]))
            build = None
        else:
            build = last_successful_build_of_month(teamcity, month)
            if build is None:
                print("##teamcity[buildProblem description='No successful %s build finished in %s']" % (BUILD_TYPE, month))
                sys.exit(1)
        daily, description = False, "the last successful build of %s" % month

    if build is not None:
        release(teamcity, releases, build, daily, description, args.dry_run)
        if not args.dry_run:
            releases = find_releases(teamcity)
    write_metadata(args.output_dir, releases, teamcity)


def release(teamcity, releases, build, daily, description, dry_run):
    artifacts = teamcity.artifact_names(build["id"])
    # A build whose installers were never built, or already cleaned up, would publish an empty release
    if not any(INSTALLER.match(name) for name in artifacts):
        print("##teamcity[buildProblem description='Build %s, %s, has no installers to release']" % (build["id"], description))
        sys.exit(1)
    version = read_version(teamcity, build["id"], artifacts)
    tag = release_tag(version, daily)
    if tag is None:
        print("##teamcity[buildProblem description='Build %s, %s, has an unrecognized installer version: %s']" % (build["id"], description, version))
        sys.exit(1)
    holder = next((r for r in releases if r["tag"] == tag), None)
    if holder:
        # The same commit, built again and released the same way
        print("##teamcity[message text='Not releasing build %s, %s: build %s is already %s' status='WARNING']"
              % (build["id"], description, holder["buildId"], tag))
        return
    print("%s and tag build %s (%s, finished %s), %s, as %s" % ("Dry run: would pin" if dry_run else "Pinning",
          build["id"], build.get("number"), build.get("finishDate"), description, tag))
    if not dry_run:
        teamcity.pin_and_tag(build["id"], tag, "Monthly release channel: %s" % description)


def release_tag(version, daily):
    """Returns release-[daily-]YYYY-MM-DD-sha for an installer version, or None if it is not one."""
    match = INSTALLER_VERSION.match(version)
    if not match:
        return None
    date = datetime.date(2000 + int(match.group(1)), 1, 1) + datetime.timedelta(days=int(match.group(2)) - 1)
    return "release-%s%s-%s" % ("daily-" if daily else "", date.isoformat(), match.group(3))


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

    def artifact_names(self, build_id):
        files = self.request("GET", "/builds/id:%s?fields=artifacts(file(name))" % build_id).get("artifacts") or {}
        return [f["name"] for f in files.get("file", [])]

    def artifact_text(self, build_id, name):
        # TeamCity redirects artifact content to S3, which rejects a forwarded Authorization header,
        # so read it as a guest; the download page links to release artifacts as a guest too
        url = "%s/guestAuth/app/rest/builds/id:%s/artifacts/content/%s" % (TEAMCITY_URL, build_id, name)
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read().decode("utf-8")

    def pin_and_tag(self, build_id, tag, comment):
        self.request("PUT", "/builds/id:%s/pinInfo" % build_id, {"status": True, "comment": {"text": comment}})
        self.request("POST", "/builds/id:%s/tags" % build_id, {"tag": [{"name": tag}]})


def previous_month(teamcity_time):
    year, month = int(teamcity_time[0:4]), int(teamcity_time[4:6])
    first_of_month = datetime.date(year, month, 1)
    return (first_of_month - datetime.timedelta(days=1)).strftime("%Y-%m")


def find_releases(teamcity):
    """Returns a release for every release tag on a pinned, successful bt83 build."""
    fields = "id,status,finishDate,defaultBranch,tags(tag(name)),revisions(revision(version)),properties(property(name,value)),artifacts(file(name))"
    # lookupLimit: by default TeamCity only searches the most recent builds, and bt83 also builds
    # every PR, so years-old releases would silently drop out of the archive
    locator = "buildType:%s,branch:default:any,pinned:true,status:SUCCESS,count:1000,lookupLimit:1000000" % BUILD_TYPE
    releases = []
    for build in teamcity.builds(locator, fields):
        for tag in (build.get("tags") or {}).get("tag", []):
            match = RELEASE_TAG.match(tag.get("name", ""))
            if match:
                releases.append(to_release(tag["name"], bool(match.group(1)), match.group(2), build))
    return releases


def to_release(tag, daily, date, build):
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
    return {"tag": tag, "date": date, "daily": daily, "buildId": build["id"], "commit": commit, "artifacts": artifacts,
            # Not published: orders releases of the same date
            "_finishDate": build.get("finishDate", "")}


def release_order(release):
    # Newest last: by commit date, then by when the build finished, then a month's release after a
    # daily release of the same build
    return (release["date"], parse_teamcity_time(release["_finishDate"]), 0 if release["daily"] else 1)


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


def read_version(teamcity, build_id, artifacts):
    # The version file is the published contract for the installer version; its name format is
    # not (see scripts/installer/build.ps1). The C++ build's VERSION separates the parts with spaces.
    for name in VERSION_FILES:
        if name in artifacts:
            return ".".join(teamcity.artifact_text(build_id, name).split()) if name == "VERSION" \
                else teamcity.artifact_text(build_id, name).strip()
    return ""


def write_metadata(output_dir, releases, teamcity):
    os.makedirs(output_dir, exist_ok=True)
    ordered = []
    for release in sorted(releases, key=release_order, reverse=True):
        published = {key: value for key, value in release.items() if not key.startswith("_")}
        published["version"] = read_version(teamcity, release["buildId"], release["artifacts"])
        ordered.append(published)
    with open(os.path.join(output_dir, BUILD_TYPE + "-archive.json"), "w", newline="\n") as f:
        json.dump({"releases": ordered}, f, indent=1)
    if ordered:
        latest = ordered[0]
        with open(os.path.join(output_dir, BUILD_TYPE + "-monthly.xml"), "w", newline="\n") as f:
            f.write("<artifacts>")
            for name in latest["artifacts"]:
                f.write("<artifact>/guestAuth/app/rest/builds/id:%s/artifacts/content/%s</artifact>" % (latest["buildId"], name))
            f.write("</artifacts>")
    print("Wrote %d releases to %s; latest: %s" % (len(ordered), output_dir, ordered[0]["tag"] if ordered else "none"))


if __name__ == "__main__":
    main()
