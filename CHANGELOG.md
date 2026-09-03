# Changelog

## 0.1.6

- Automatically mirror recipes currently registered on the standard
  `BiofuelRefinery`, including recipes added through either `ThingDef.recipes`
  or `RecipeDef.recipeUsers`.
- Automatically expose safe fecal-sludge recipes from the standard refinery or
  DBH burn pit as pipe-backed bills on the piped refinery. Their sewage cost is
  calculated from the recipe and current bill instead of being fixed at 75.
- Keep ordinary inherited recipes on their original physical ingredients and
  leave every third-party `RecipeDef` unchanged outside the piped refinery.
- Reject unsafe automatic substitutions that use custom recipe workers,
  unfinished items, special products, or ingredient-dependent stuff products.
- Scale the refinery's sewage buffer to the largest discovered pipe-backed
  recipe and preserve fail-closed reservation and rollback behavior.
- Remove per-Mod load-order hints; automatic compatibility no longer depends on
  package-ID allowlists.

## 0.1.5

- Prepared the first publishable RimWorld 1.6 package.
- Added direct sewage routing from the fullest underground sewage pit to the
  piped composter and piped biofuel refinery.
- Added overflow recovery and timeout spill handling for the pit, composter and
  refinery without truncating resources during rollback.
- Added complete-integer emergency extraction with consistent work-giver and
  job checks for fractional sewage remnants.
- Added fail-closed handling for malformed or disabled refinery bills and
  removed duplicate recipe registration.
- Added robust blocked-sewer alert filtering that preserves genuine outlet
  failures.
- Added runtime sewage-pump power setting updates. A zero-power composter no
  longer displays a false missing-power overlay, while the refinery retains its
  170 W processing requirement.
- Kept the developer sewage injector and debug diagnostics available for
  troubleshooting.
- Cleaned up component structure, state access and resource settlement paths.

## Earlier development builds

Earlier `0.1.5-test` builds were development artifacts and are superseded by
this package. Existing saves remain compatible with the same package ID.
