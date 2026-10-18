# Arch Linux packaging

## Dependency compatibility (#292)

Build XerahS and its recorded submodules as one revision. Do not clone ImageEditor
or Omacut at independent branch heads, and do not use `git submodule update --remote`.
The editor API can change between parent commits; the reported missing gradient
properties were caused by mixing incompatible revisions.

The repository-owned `build/linux/aur/xerahs-git/PKGBUILD` initializes submodules with
`--checkout` and checks the complete recursive dependency state before compilation.
A missing or advanced dependency fails early with an actionable error, rather than
failing later inside the image effect importer.

For an existing development checkout, preserve your local dependency changes before
running `git submodule update --init --recursive --checkout`. Build packages in a
clean checkout. If an AUR helper cached the older community recipe that clones editor
repositories independently, choose a clean build after updating the recipe, or use
the repository-owned recipe described here. The public AUR listing is maintained
separately; a commit to this repository does not update that listing.

Validation: `bash build/tests/verify-submodules.sh` exercises pinned, independently
advanced and uninitialized dependencies using disposable local Git repositories.
