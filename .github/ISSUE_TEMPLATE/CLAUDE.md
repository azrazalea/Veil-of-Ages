# Issue Templates Directory

## Purpose

Contains GitHub issue templates that provide structured formats for bug reports and feature requests.

## Files

### bug_report.md
Template for reporting bugs. Ensures reporters include:
- Clear description of the issue
- Steps to reproduce
- Expected behavior
- Actual behavior
- Screenshots if applicable
- Environment details (OS, Godot version, etc.)

### feature_request.md
Template for requesting new features. Guides users to describe:
- The problem or need
- Proposed solution
- Alternative solutions considered
- Additional context

## Usage

These templates appear automatically when users click "New Issue" on GitHub. Users select the appropriate template, which pre-fills the issue body with prompts.

## Customization

To add new templates:
1. Create a new `.md` file in this directory
2. Add YAML front matter with `name`, `about`, and `labels` fields
3. Add the template body with markdown sections

Example front matter:
```yaml
---
name: Bug report
about: Create a report to help us improve
labels: bug
---
```
