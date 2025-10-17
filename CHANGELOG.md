# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-10-17
### Added
- Initial public release of **ChatGPT Context Exporter**.
- Stage/unstage system for `.cs` and `.md` files.
- Export of `Code.txt` and `Instructions.md` files.
- Git integration with `git log` + `git diff` between selected dates.
- Filters for `Author` and `Grep` in Git tab.
- Quick range options: Last 24h, Last 7 Days, This Month, All Time.
- Prompt window with three modes: New, Ongoing, Don’t Open.
- Persistent settings using `EditorPrefs`.
- Modular structure with separate classes for each feature.

### Changed
- Optimized file IO with `StreamWriter` for exports.
- Improved GUI layout for better workflow separation.
- Cleaned up naming conventions and organized constants.

### Fixed
- Fixed GUI layout mismatches and scroll issues.
- Handled git diff edge cases (invalid date, empty repo, etc.).
