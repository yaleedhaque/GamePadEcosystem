# Contributing to GamePadEcosystem

Thanks for your interest in contributing! Here's how to get started.

## How to Contribute

1. **Fork** this repository
2. **Clone** your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/GamePadEcosystem.git
   ```
3. **Create** a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
4. **Make** your changes
5. **Build and test** your changes (see below)
6. **Commit** with a clear message:
   ```bash
   git commit -m "Add: description of your change"
   ```
7. **Push** to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
8. **Open a Pull Request** against `main`

## Building

### Windows Server
```bash
cd server\src\GamePadServer
dotnet build -c Release
```

### Android Client
```bash
cd client
./gradlew assembleDebug
```

## Guidelines

- Follow existing code style and naming conventions
- Write clear, descriptive commit messages
- Test on Windows 11 + Android device before submitting
- Update documentation if adding new features
- Keep pull requests focused — one feature or fix per PR

## Reporting Bugs

Open an issue with:
- Steps to reproduce
- Expected vs actual behavior
- Your OS version, .NET version, Android version
- Any error output or logs

## Code of Conduct

Be respectful, constructive, and professional.
