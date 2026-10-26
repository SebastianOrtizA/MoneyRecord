---
name: release-moneyrecord
description: Prepare and publish a MoneyRecord release (release notes in three languages, PR, then the v* tag that builds the APK/AAB and uploads to Google Play closed testing). Use when asked to release, cut a version, tag, write the release notes or the "What's new" text for a version.
---

A release = a **notes PR**, then a **tag**. The tag build publishes the GitHub Release and uploads the `.aab` to Play's
closed testing track, so the tag is outward-facing: **ask the owner before pushing it**, even if they asked for the notes.

## 1. Pick the version

- Last tag: `git tag --sort=-v:refname | head -3`. Versions are `x.y.z` (a suffix like `-rc1` marks a pre-release).
  The version name comes from the tag; the version code is the CI run number.
- Ask the owner for the number when it is not given (patch for fixes, minor for features).

## 2. Collect what changed

`git log <last-tag>..main --oneline` (PR titles carry the stage). Keep only what a user notices; skip refactors,
tests, docs and CI. Write in the style of the previous notes (`git show <last-tag>:fastlane/metadata/android/en-US/changelogs/history.txt`).

## 3. Write the notes in all three locales

Folders: `fastlane/metadata/android/{en-US,es-ES,es-419}/changelogs/`. es-ES and es-419 are the same text unless a
word differs by region. Edit with the Edit tool (a shell `perl`/`sed` mangles `\n` and non-ASCII).

- `default.txt` = Play's notes for this release: plain sentences, **500 characters at most** (a test enforces it).
- `history.txt` = the in-app "What's new" text: add a new `## x.y.z` section at the **top**, a blank line after it,
  then one `- change` line per change. The versions listed must match in every locale (a test enforces it). The pop-up
  shows every section newer than the last one the user saw, and a section older than the running version keeps its heading,
  so a forgotten section is visible rather than silent.

## 4. PR

Branch `release-notes-x.y.z` from main, run `dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj` from PowerShell,
commit "Release notes for x.y.z", open a PR (the owner reviews via PR comments and merges).

## 5. Tag (only after the owner says the PR is merged and agrees to release)

```bash
git checkout main && git pull
git tag vX.Y.Z && git push origin vX.Y.Z
```

Then watch `gh run list --limit 3`; the build fails unless the release keystore secrets are set. Promoting the Play
release to production stays manual in Play Console. Optionally bump `ApplicationDisplayVersion` in
`MoneyRecord/MoneyRecord.csproj` afterwards so untagged builds are distinguishable.
