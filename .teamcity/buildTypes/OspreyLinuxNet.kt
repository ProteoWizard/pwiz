package buildTypes

import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.exec
import jetbrains.buildServer.configs.kotlin.failureConditions.BuildFailureOnText
import jetbrains.buildServer.configs.kotlin.failureConditions.failOnText

/*
 * Osprey built and tested on Linux - the Osprey Windows .NET build step, run with Core Linux
 * .NET's platform wiring.
 *
 * What each half contributes, and why it is not a free mix: every Windows config here carries
 * the WindowsBase + WindowsCoreRelease templates and declares NO vcs block, because WindowsBase
 * supplies the VCS root. Both non-Windows configs (Core Linux .NET, the Wine container) carry
 * neither Windows template and declare their own vcs instead. This follows that split, so the
 * templates and the vcs block below are taken from Core Linux .NET and only the build step and
 * artifact rule come from Osprey.
 *
 * Osprey needs no Linux-specific product changes: every project is plain net10.0 and
 * ProteowizardWrapper is net10.0-only, so the same solution builds. tcbuild.sh drives the same
 * build.ps1 / package.ps1 the Windows config does - pwsh is the project standard for Osprey
 * scripting - differing only in skipping the Windows-only wix/.msi step and packaging linux-x64.
 */
object OspreyLinuxNet : BuildType({
    // New config in this subproject, so TeamCity requires the parent project id as a prefix.
    // Osprey Windows .NET predates the subproject and kept its original id when it was moved.
    id = AbsoluteId("ProteoWizard_VersionedConfigs_OspreyLinuxNet")
    name = "Osprey Linux .NET"

    templates(AbsoluteId("ProteoWizard_GitHubReportingConfig"),
              AbsoluteId("ProteoWizard_RemoteAccountTestParameters"))

    // package.ps1 -TeamCity emits the publishArtifacts service messages itself, so the artifact
    // set lives with the script rather than in server-side config (as Osprey Windows .NET does).
    artifactRules = "# Configure artifacts in tcbuild.sh"

    vcs {
        root(AbsoluteId("ProteoWizard_PwizGithubMasterWithPRs"))
        checkoutMode = CheckoutMode.ON_AGENT
    }

    steps {
        exec {
            name = "Build and test Osprey (Linux)"
            path = "pwiz_tools/Osprey/tcbuild.sh"
        }
    }

    failureConditions {
        // Matches Core Linux .NET: a spot-instance kill and a stack overflow both surface as
        // log text rather than a non-zero exit, so without these the build can pass having run
        // nothing.
        failOnText {
            id = "OSPREY_LINUX_SPOT"
            conditionType = BuildFailureOnText.ConditionType.CONTAINS
            pattern = "The spot instance is scheduled for termination"
            failureMessage = "AWS terminated the spot instance early. Retrigger the build manually from TeamCity."
            reverse = false
            stopBuildOnFailure = true
        }
    }

    requirements {
        contains("teamcity.agent.jvm.os.name", "Linux")
    }

    // No triggers block: smartBuildTrigger.py queues this over REST, as it does for every
    // build config here. See note 3 in the file header.
})
