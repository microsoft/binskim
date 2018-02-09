# Topics Needing Documentation

Our documentation is not perfect--this file consists of a list of topics that currently aren't or are not well documented.

## Usage/Configuration

### More Detailed Results/Triage Guidance

It'd be nice to better document *why* particular mitigations are important to enable (more detail than currently, at least).

If possible, there are also areas where more detailed steps for discovering the best way to mitigate a problem in more complex project scenarios (e.x. "Here's a brief guide on how to find where the build flags are likely coming from", "Here are the sorts of scenarios it may be important to test/check before re-releasing after enabling this flag", etc.).  It's true that all of the flags *should* basically be a non-breaking change for well formed code that's entirely C/C++, but that certainly does not describe all code.

(These vary in importance.  We have a decent, high level 'why' for each result, as well as a general 'how to fix it using VC++'.)

### Plugin System

Currently not really documented outside of code in the SARIF SDK.

### Rule Configuration

Although a user can generate valid configuration via the `ExportConfig` command, this system is not well documented outside of code in the SARIF SDK--both from a "modifying config for usage" and "developing new rules" perspective.

## Developer Documentation

### Coding Style

We should document the coding style expectations for BinSkim, including naming conventions for projects/namespaces/classes/methods/etc., spacing conventions, etc.

### Rule Writing

A document on how to write a new rule would be valuable for new contributors.  Also, the philosophy on errors versus warnings, importance of good results, etc. should be documented.
