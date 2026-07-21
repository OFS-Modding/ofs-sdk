# Computer apps and hired employees

SDK 0.3 can add scene-bound applications to the vanilla factory computer and
query, reserve, or release persistent hires from `EmployeeManager`.

The complete guide and examples live in
[`docs/computer-apps-and-employees.md`](../computer-apps-and-employees.md).

Key rules:

- register computer apps after the `Factory` scene loads;
- dispose registrations on unload;
- assign and release hires only while the local Mirror server is active;
- use qualified employee IDs, never display names;
- release assignments on every success, failure, and teardown path.
