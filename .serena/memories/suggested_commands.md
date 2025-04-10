# Suggested Commands

## Git Commands
```
# Check repository status
git status

# View changes
git diff
git diff --staged

# Stage files
git add <filename>
git add -A  # Stage all changes

# Commit changes
git commit -m "Descriptive message"

# Pull latest changes
git pull

# Push changes
git push

# View commit history
git log
git log --oneline --graph

# Create and switch to a new branch
git checkout -b <branch-name>

# Switch between branches
git checkout <branch-name>

# Merge branches
git merge <branch-name>
```

## Godot-Related Commands
```
# Run the Godot editor
godot -e  # Open the Godot editor

# Run the game
godot  # Run the project
```

## .NET Commands
```
# Build the project
dotnet build

# Run specific C# analyzers
dotnet format

# Install additional NuGet packages
dotnet add package <package-name>
```

## Windows Command-Line Utilities
```
# List files and directories
dir
dir /s  # Recursive listing

# Change directory
cd <directory>
cd ..  # Move up one level

# Create directory
mkdir <directory-name>

# Delete file
del <filename>

# Delete directory
rmdir <directory-name>
rmdir /s /q <directory-name>  # Force delete with subdirectories

# Find text in files
findstr /s /i "<text>" <path>

# Find files by name
dir "*<pattern>*" /s

# Copy files
copy <source> <destination>
xcopy <source> <destination> /e /i  # Copy directories with subdirectories
```

## Common Development Tasks
```
# Format code
dotnet format

# Run all tests (when tests are implemented)
dotnet test

# Build for release
dotnet build -c Release
```

These commands cover the basic operations for developing, testing, and managing the Veil of Ages project on a Windows system.