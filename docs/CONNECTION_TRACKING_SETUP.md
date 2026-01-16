# Connection Tracking Setup Guide

## Issue
The firewall states page shows no data because connection tracking is not enabled or available on the system.

## Diagnosis
- `/proc/net/nf_conntrack` doesn't exist
- `conntrack` command not found

## Solutions

### Option 1: Enable Connection Tracking (Recommended)

Connection tracking is required for stateful firewall functionality. It should be enabled automatically when nftables rules are applied, but we need to ensure the kernel module is loaded.

#### Check if module is loaded:
```bash
lsmod | grep nf_conntrack
```

#### Load the module:
```bash
sudo modprobe nf_conntrack
```

#### Make it persistent (add to `/etc/modules` or `/etc/modules-load.d/`):
```bash
echo "nf_conntrack" | sudo tee -a /etc/modules
```

#### Verify it's working:
```bash
ls -la /proc/net/nf_conntrack
# Should show the file exists
```

### Option 2: Install conntrack-tools (Optional but Recommended)

The `conntrack` command provides better formatted output:

```bash
# Debian/Ubuntu
sudo apt-get install conntrack

# RHEL/CentOS
sudo yum install conntrack-tools
```

### Option 3: Check nftables Connection Tracking

Since the system uses nftables, connection tracking should work automatically. Verify:

```bash
# Check if nftables is using connection tracking
sudo nft list ruleset | grep "ct state"
```

If you see `ct state` rules, connection tracking should be active.

### Option 4: Alternative - Use ss/netstat (Limited)

If connection tracking can't be enabled, we could use `ss` or `netstat` to show active connections, but this won't show firewall states - only socket connections.

## Implementation Note

The firewall rules already include connection tracking:
- `ct state invalid drop`
- `ct state established,related accept`

So connection tracking should be working. The issue might be:
1. Kernel module not loaded
2. System needs a reboot after nftables was configured
3. Connection tracking needs to be explicitly enabled

## Next Steps

1. Load the nf_conntrack module
2. Verify `/proc/net/nf_conntrack` appears
3. Generate some network traffic (ping, curl, etc.)
4. Refresh the States page
