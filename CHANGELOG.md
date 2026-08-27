# Changelog

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
