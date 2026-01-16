#!/usr/bin/env python3
import re
import sys

def collapse_log(input_text):
    lines = input_text.strip().split('\n')
    seen = set()
    result = []
    
    for line in lines:
        # Remove tick number prefix like [123] or [0]
        stripped = re.sub(r'^\[\d+\]\s*', '', line)
        
        # If line doesn't start with [number], keep it as is
        if stripped == line:
            # Check if it's a line without tick prefix
            if line not in seen:
                seen.add(line)
                result.append(line)
        else:
            # It had a tick prefix, use stripped version
            if stripped not in seen:
                seen.add(stripped)
                result.append(stripped)
    
    return '\n'.join(result)

# Read from stdin or file
if len(sys.argv) > 1:
    with open(sys.argv[1], 'r') as f:
        text = f.read()
else:
    text = sys.stdin.read()

print(collapse_log(text))
