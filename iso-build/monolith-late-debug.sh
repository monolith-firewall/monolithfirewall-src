#!/bin/sh
set -eu

TARGET="/target"
LOG="${TARGET}/var/log/monolith-install.log"

mkdir -p "${TARGET}/var/log" || true
touch "$LOG" || true

echo "=== monolith-late-debug.sh starting ===" >> "$LOG"
date -u >> "$LOG" || true

echo "--- installer mounts ---" >> "$LOG"
mount >> "$LOG" 2>&1 || true

echo "--- /cdrom listing ---" >> "$LOG"
ls -la /cdrom >> "$LOG" 2>&1 || true
ls -la /cdrom/monolith-debs 2>/dev/null | head -50 >> "$LOG" || true
ls -la /cdrom/monolith-packages 2>/dev/null | head -50 >> "$LOG" || true

CDROM_PATH=""
for path in /cdrom /media/cdrom /mnt /run/live/medium; do
    if [ -d "$path/monolith-debs" ] || [ -d "$path/monolith-packages" ]; then
        CDROM_PATH="$path"
        break
    fi
done

echo "Detected CDROM_PATH=${CDROM_PATH:-<none>}" >> "$LOG"

mkdir -p "${TARGET}/var/lib/monolith-firewall/packages" || true
mkdir -p "${TARGET}/var/cache/monolith-debs" || true

if [ -n "${CDROM_PATH}" ] && [ -d "${CDROM_PATH}/monolith-packages" ]; then
    echo "--- copying .mfwpkg packages ---" >> "$LOG"
    COPIED=0
    for pkg in "${CDROM_PATH}"/monolith-packages/*.mfwpkg; do
        if [ -f "$pkg" ]; then
            cp "$pkg" "${TARGET}/var/lib/monolith-firewall/packages/" && COPIED=$((COPIED+1)) || true
            echo "copied mfwpkg: $(basename "$pkg")" >> "$LOG"
        fi
    done
    echo "mfwpkg copied=${COPIED}" >> "$LOG"
fi

if [ -n "${CDROM_PATH}" ] && [ -d "${CDROM_PATH}/monolith-debs" ]; then
    echo "--- copying monolith-debs repo ---" >> "$LOG"
    cp "${CDROM_PATH}/monolith-debs/"* "${TARGET}/var/cache/monolith-debs/" 2>/dev/null || true
    ls -la "${TARGET}/var/cache/monolith-debs" | head -80 >> "$LOG" 2>&1 || true
fi

# Configure APT sources in target: keep default CDROM source, and add monolith offline repo
mkdir -p "${TARGET}/etc/apt/sources.list.d" || true
echo "deb [trusted=yes] file:/var/cache/monolith-debs ./" > "${TARGET}/etc/apt/sources.list.d/monolith-offline.list" || true

echo "--- target apt sources ---" >> "$LOG"
ls -la "${TARGET}/etc/apt/sources.list"* "${TARGET}/etc/apt/sources.list.d" 2>>"$LOG" || true
sed -n '1,200p' "${TARGET}/etc/apt/sources.list" >> "$LOG" 2>&1 || true
sed -n '1,200p' "${TARGET}/etc/apt/sources.list.d/monolith-offline.list" >> "$LOG" 2>&1 || true

echo "--- running target apt-get update ---" >> "$LOG"
in-target sh -c 'set -eux; DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::Languages=none update' >> "$LOG" 2>&1 || true

echo "--- running target apt-get install (no recommends) ---" >> "$LOG"
in-target sh -c 'set -eux; DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::Languages=none install -y --no-install-recommends openssh-server openssh-client monolith-firewall' >> "$LOG" 2>&1 || true

echo "--- dpkg status (target) ---" >> "$LOG"
in-target sh -c 'dpkg -l | egrep "openssh|monolith-firewall" || true' >> "$LOG" 2>&1 || true

echo "--- enabling services (if present) ---" >> "$LOG"
for svc in monolith-firewall-core.service monolith-firewall-webui.service monolith-firstboot.service ssh.service sshd.service; do
    if [ -f "${TARGET}/lib/systemd/system/${svc}" ] || [ -f "${TARGET}/etc/systemd/system/${svc}" ]; then
        in-target systemctl enable "${svc}" >> "$LOG" 2>&1 || true
        echo "enabled: ${svc}" >> "$LOG"
    else
        echo "missing: ${svc}" >> "$LOG"
    fi
done

echo "--- sshd_config check ---" >> "$LOG"
if [ -f "${TARGET}/etc/ssh/sshd_config" ]; then
    sed -i 's/#PermitRootLogin.*/PermitRootLogin yes/' "${TARGET}/etc/ssh/sshd_config" || true
    sed -i 's/^PermitRootLogin.*/PermitRootLogin yes/' "${TARGET}/etc/ssh/sshd_config" || true
    echo "PermitRootLogin set" >> "$LOG"
else
    echo "WARNING: sshd_config not found" >> "$LOG"
fi

# Capture some installer logs into the target for later inspection
mkdir -p "${TARGET}/var/log/installer-debug" || true
cp /var/log/syslog "${TARGET}/var/log/installer-debug/syslog" 2>/dev/null || true
cp /var/log/partman "${TARGET}/var/log/installer-debug/partman" 2>/dev/null || true
cp /var/log/daemon.log "${TARGET}/var/log/installer-debug/daemon.log" 2>/dev/null || true

echo "=== monolith-late-debug.sh finished ===" >> "$LOG"
date -u >> "$LOG" || true

exit 0

