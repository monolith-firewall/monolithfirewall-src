#!/bin/bash
set -e

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - Dependency Setup"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$SCRIPT_DIR"
TMP_DIR="$ROOT_DIR/tmp"

# Repositories to clone (format: URL|DIRECTORY_NAME)
REPOS=(
    "https://github.com/Media2A/CodeLogic3|CodeLogic3"
    "https://github.com/Media2A/CodeLogic3.Libs|CodeLogic3.Libs"
    "https://github.com/monolith-firewall/monolithfirewall-packages|monolithfirewall-packages"
)

# Cleanup old locations
CLEANUP_DIRS=(
    "$ROOT_DIR/../libs"
)

# Check if git is installed
if ! command -v git &> /dev/null; then
    echo "ERROR: git is not installed"
    echo "Install with: sudo apt-get install -y git"
    exit 1
fi

# Cleanup old dependency locations
echo "→ Cleaning up old dependency locations..."
for cleanup_dir in "${CLEANUP_DIRS[@]}"; do
    if [ -d "$cleanup_dir" ]; then
        echo "  Removing old directory: $cleanup_dir"
        rm -rf "$cleanup_dir"
    fi
done

# Create tmp directory
echo "→ Creating tmp directory..."
mkdir -p "$TMP_DIR"
cd "$TMP_DIR"

# Function to clone or update a repository
clone_or_update() {
    local repo_url="$1"
    local repo_name="$2"
    local repo_path="$TMP_DIR/$repo_name"
    
    if [ -d "$repo_path" ]; then
        echo "  Updating $repo_name..."
        cd "$repo_path"
        
        # Check if it's a git repository
        if [ -d ".git" ]; then
            # Fetch latest changes
            git fetch origin 2>&1 | grep -v "^From" || true
            
            # Get current branch
            CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "main")
            
            # Pull latest changes
            if git pull origin "$CURRENT_BRANCH" 2>&1 | grep -v "^Already up to date"; then
                echo "    ✓ Updated $repo_name"
            else
                echo "    ✓ $repo_name is already up to date"
            fi
        else
            echo "    WARNING: $repo_path exists but is not a git repository"
            echo "    Removing and re-cloning..."
            cd "$TMP_DIR"
            rm -rf "$repo_path"
            git clone "$repo_url" "$repo_name"
            echo "    ✓ Cloned $repo_name"
        fi
    else
        echo "  Cloning $repo_name..."
        git clone "$repo_url" "$repo_name"
        echo "    ✓ Cloned $repo_name"
    fi
    
    cd "$TMP_DIR"
}

# Clone or update each repository
echo "→ Cloning/updating repositories..."
for repo_info in "${REPOS[@]}"; do
    IFS='|' read -r repo_url repo_name <<< "$repo_info"
    clone_or_update "$repo_url" "$repo_name"
done

echo ""
echo "→ Verifying repositories..."
MISSING=0
for repo_info in "${REPOS[@]}"; do
    IFS='|' read -r repo_url repo_name <<< "$repo_info"
    if [ ! -d "$TMP_DIR/$repo_name" ]; then
        echo "  ✗ Missing: $repo_name"
        MISSING=1
    else
        echo "  ✓ Found: $repo_name"
    fi
done

if [ $MISSING -eq 1 ]; then
    echo ""
    echo "ERROR: Some repositories failed to clone"
    exit 1
fi

# Verify CodeLogic3 structure
echo ""
echo "→ Verifying repository structure..."
if [ ! -f "$TMP_DIR/CodeLogic3/src/CodeLogic.csproj" ]; then
    echo "  ✗ ERROR: CodeLogic3/src/CodeLogic.csproj not found"
    exit 1
fi
echo "  ✓ CodeLogic3 structure verified"

if [ ! -f "$TMP_DIR/CodeLogic3.Libs/CL.SQLite/CL.SQLite.csproj" ]; then
    echo "  ✗ ERROR: CodeLogic3.Libs/CL.SQLite/CL.SQLite.csproj not found"
    exit 1
fi
echo "  ✓ CodeLogic3.Libs structure verified"

if [ ! -d "$TMP_DIR/monolithfirewall-packages" ]; then
    echo "  ✗ ERROR: monolithfirewall-packages directory not found"
    exit 1
fi
echo "  ✓ monolithfirewall-packages structure verified"

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  Dependencies Setup Complete!"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Repositories cloned/updated in: $TMP_DIR"
echo ""
echo "Project references have been updated to use:"
echo "  - ../tmp/CodeLogic3/ (from src/ projects)"
echo "  - ../tmp/CodeLogic3.Libs/ (from src/ projects)"
echo "  - ../../tmp/CodeLogic3/ (from tmp/monolithfirewall-packages/)"
echo ""
echo "Build scripts will look for packages in: tmp/monolithfirewall-packages/"
echo ""
echo "You can now build the project!"
echo ""
