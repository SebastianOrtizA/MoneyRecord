---
name: release-moneyrecord
description: Prepare and publish a MoneyRecord release (release notes in six locales, PR, then the release branch that builds the APK/AAB and uploads to Google Play closed testing). Use when asked to release, cut a version, tag, write the release notes or the "What's new" text for a version.
---

A release = a **notes PR**, then a **release branch**. A build of a release branch publishes the GitHub Release and uploads the `.aab` to Play's
closed testing track, so it is outward-facing: **ask the owner before cutting it or merging a backport**, even if they asked for the notes.

## 1. Pick the version

- Last tag: `git tag --sort=-v:refname | head -3`. Versions are `x.y.z`. Major.minor = the `release/x.y` branch; the patch is
  the next free `vx.y.N` tag on it; the version code is the CI run number. Tags are never pushed by hand.
- A new minor or major: the version in `MoneyRecord.csproj` on main (e.g. 0.19.0). A fix: the next patch of a shipped line.

## 2. Collect what changed

`git log <last-tag>..main --oneline` (PR titles carry the stage). Keep only what a user notices; skip refactors,
tests, docs and CI. Write in the style of the previous notes (`git show <last-tag>:fastlane/metadata/android/en-US/changelogs/history.txt`).

## 3. Write the notes in all locales

Folders: `fastlane/metadata/android/{en-US,es-ES,es-419,pt-BR,fr-FR,it-IT}/changelogs/`. es-ES and es-419 are the same text unless a
word differs by region. Write en-US first, then translate it into the other five (glossary: `/translate-moneyrecord`). Edit with the Edit tool (a shell `perl`/`sed` mangles `\n` and non-ASCII).

- `default.txt` = Play's notes for this release: plain sentences, **500 characters at most** (a test enforces it).
- `history.txt` = the in-app "What's new" text: add a new `## x.y.z` section at the **top**, a blank line after it,
  then one `- change` line per change. The versions listed must match in every locale (a test enforces it). The pop-up
  shows every section newer than the last one the user saw, and a section older than the running version keeps its heading,
  so a forgotten section is visible rather than silent.

## 4. PR

Branch `release-notes-x.y.z` from main, run `dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj` from PowerShell,
commit "Release notes for x.y.z", open a PR (the owner reviews via PR comments and merges).

## 5. Publish (only after the owner says the notes PR is merged and agrees to release)

A build of `release/<major>.<minor>` publishes (tag `v<major>.<minor>.<patch>`, GitHub Release, Play upload) only when the
top `## ` section of the en-US `history.txt` is the version it computed; otherwise it just builds and says which notes
are missing. Never push a tag or the branch by hand.

- **New minor / major**: notes PR for `x.y.0` merged to main, then Actions → **Cut release** (or
  `gh workflow run cut-release.yml -f next=minor`). It creates `release/x.y` from main, opens a PR moving main's csproj
  to the next line, and builds `x.y.0`.
- **Fix to a shipped line**: PR to main labelled `backport release/x.y` (create the label once per line:
  `gh label create "backport release/x.y"`). `release/*` only takes changes through a pull request (ruleset "Review
  Required"; a push is refused with GH013), so on merge Backport cherry-picks it onto `backport/<PR>-to-release-x.y` and
  **opens a PR to the release branch**; merging that PR builds the branch. The notes PR for `x.y.N` (also labelled) opens
  the PR whose merge publishes the patch, so merge the fix backports first, the notes backport last. A PR opened with
  `GITHUB_TOKEN` runs no Build & Test; the push after its merge does. A conflict is reported on the PR with the commands
  to make the backport PR by hand (`git checkout -b backport/<PR>-to-release-x.y origin/release/x.y`, `git cherry-pick
  <squash sha>`, push, `gh pr create --base release/x.y`).

Watch `gh run list --limit 3`; the build fails unless the release keystore secrets are set. Promoting the Play release
to production stays manual in Play Console.
