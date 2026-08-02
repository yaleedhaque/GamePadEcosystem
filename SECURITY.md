# Security Policy

## Supported Versions

Only the latest release receives security fixes.

| Version | Supported |
| ------- | --------- |
| 1.1.x   | yes       |
| older   | no        |

## Reporting a Vulnerability

This is a personal project. Security issues are taken seriously and handled as quickly as possible.

1. Do not open a public issue for security-sensitive bugs.
2. Report via email at [yaleedhaque@gmail.com](mailto:yaleedhaque@gmail.com) or open a private security advisory at <https://github.com/yaleedhaque/GamePadEcosystem/security/advisories/new>.
3. Include the affected version, a description of the issue, and a minimal reproduction if possible.

You should receive a response within a few days. Please do not disclose the issue publicly until it has been addressed.

## Notes

- The server listens on UDP 9876/9878 and is designed for trusted LAN use. Do not expose these ports to untrusted networks without additional hardening.
- The Android client communicates only on the local network.
