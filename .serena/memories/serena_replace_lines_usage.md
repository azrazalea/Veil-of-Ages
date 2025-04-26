# Correct Usage of replace_lines Tool in Serena

When using the `replace_lines` tool in Serena, you MUST follow this exact process:

1. Call `read_file` with EXACTLY the same start and end lines that you plan to replace
2. Then immediately call `replace_lines` with those exact same line numbers

## Common Mistakes to Avoid:
- Reading the whole file and then trying to replace a subset of lines
- Reading a larger region and then replacing only part of it
- Reading one set of lines and trying to replace a different set

## Example Correct Usage:
```
# This is CORRECT - reading and replacing the exact same lines
<function_calls>
<invoke name="read_file">
<parameter name="start_line">10