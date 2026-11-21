#!/usr/bin/env bash
# Sourced by helpers. Profiles keep separate EEPROM/logs outside the checkout.
profile="${1:-standard}"
case "$profile" in
    standard) run_dir="$HOME/club-rov-sitl/run" ;;
    external-nav) run_dir="$HOME/club-rov-sitl/run-external-nav" ;;
    dvl) run_dir="$HOME/club-rov-sitl/run-dvl" ;;
    *) echo "Unknown SITL profile: $profile" >&2; exit 1 ;;
esac
