# .github Directory

## Purpose

GitHub-specific configuration and templates for the repository. Contains issue templates and potentially workflow configurations.

## Structure

```
.github/
└── ISSUE_TEMPLATE/
    ├── bug_report.md     # Template for bug reports
    └── feature_request.md # Template for feature requests
```

## Files

### ISSUE_TEMPLATE/
Contains markdown templates that GitHub uses when users create new issues:

- **bug_report.md**: Structured template for reporting bugs with fields for:
  - Description
  - Steps to reproduce
  - Expected vs actual behavior
  - Environment details

- **feature_request.md**: Template for proposing new features with fields for:
  - Problem description
  - Proposed solution
  - Alternatives considered

## Usage

When someone creates a new issue on GitHub, they'll be prompted to choose a template. The templates ensure consistent, well-structured issue reports.

## Related Documentation

- See `/CONTRIBUTING.md` for contribution guidelines
- See `/CODE_OF_CONDUCT.md` for community standards
