# Spixi

**Spixi** is a decentralized, privacy-first **communications app** built on the [Ixian Platform](https://www.ixian.io).
It combines **messaging, voice calls, file sharing, crypto wallet functionality, and Mini Apps** in a single,
cross-platform experience - with Post-Quantum Cryptography (PQC) and no central servers.

Unlike traditional messengers that rely on central servers to store keys and mediate authentication, Spixi users **hold and
control their own encryption keys locally**. Every conversation is uniquely encrypted, and identities are proven cryptographically
without passwords, central accounts, or third-party trust.

---

## 🚀 Why Spixi?

* ⚛️ **Post-Quantum Resistant Security** - Hybrid key exchange (`RSA + ECDH + Kyber/ML-KEM`) ensures confidentiality even against
future quantum threats.
* 🔒 **True End-to-End Encryption** - Dual encryption (AES + ChaCha20-Poly1305). Keys never leave the user's device, and each
contact-pair uses unique cryptographic keys. Only the sender and intended recipient can ever decrypt a message.
* 🧬 **Self-Authentication, Not Server Authentication** - In centralized systems, a username and password authenticate you to a
server, which then manages or even stores your keys. In Ixian, **your cryptographic address is your identity**, and you sign
messages yourself with your private key. No servers vouch for you - your identity and communication security are entirely under
your control.
* 🕸️ **Decentralized by Design** - Runs peer-to-peer over Ixian's **DLT** and **S2 overlay network**. Client discovery works
via cryptographic addresses and signed presence packets (not DNS, IP, or certificate authorities), ensuring authenticity and
privacy.
* 📱 **Cross-Platform** - Works on Android, iOS, Windows, and macOS.
* 💬 **Rich Communication** - Chat, group messaging, voice calls, reactions, emojis, and file sharing.
* 💸 **Built-In Crypto Wallet** - Send and receive IXI transactions directly inside the app.
* 🧩 **Extensible with Mini Apps** - Developers can build secure, in-app extensions and workflows within Spixi.

---

## 📚 Documentation

Developer documentation, build guides, and API references are available at:
👉 [docs.ixian.io](https://docs.ixian.io)

---

## 🛠️ Build Instructions

### Prerequisites

- .NET 10 SDK or later
- Visual Studio 2026 or later (with .NET MAUI workload installed)
- Git

### Cloning the Repository

First, you need to clone the repository to your local machine. Open your terminal or Git Bash and run the following command:

```bash
git clone https://github.com/ixian-platform/Ixian-Core.git
git clone https://github.com/ixian-platform/Spixi.git
cd Spixi
```

### Building and Running with Visual Studio

1. **Open the Solution:**
   - Launch Visual Studio.
   - Open the cloned repository folder and double-click on the solution file (`Spixi.sln`) to open it in Visual Studio.

2. **Restore NuGet Packages:**
   - Visual Studio should automatically restore the NuGet packages. If not, go to `Tools` > `NuGet Package Manager` > `Package Manager Console` and run:
     ```powershell
     dotnet restore
     ```

3. **Build the Solution:**
   - Build the solution by clicking on `Build` > `Build Solution` or by pressing `Ctrl+Shift+B`.

4. **Run the Application:**
   - Select the target platform (Android, iOS, Windows, etc.) from the toolbar.
   - Click on the `Start` button or press `F5` to run the application.

### Building and Running via Terminal

1. **Restore NuGet Packages:**
   - Open your terminal and navigate to the cloned repository folder:
     ```bash
     cd Spixi
     ```
   - Restore the NuGet packages by running:
     ```bash
     dotnet restore
     ```

2. **Build and Run the Application:**
   - To build and run the application on a specific platform, use the following command:
     ```bash
     dotnet build -t:Run -f net10.0-android              # For Android
     dotnet build -t:Run -f net10.0-ios                  # For iOS
     dotnet build -t:Run -f net10.0-windows10.0.19041.0 -p:Platform=x64  # For Windows
     dotnet build -t:Run -f net10.0-maccatalyst          # For macOS
     ```
   - Ensure you have the appropriate SDKs and emulators/simulators installed for the target platform.

3. **Build the Application in Release mode:**
   - To build and run the application on a specific platform, use the following command:
     ```bash
     dotnet build --configuration Release -f net10.0-android # For Android
     dotnet build --configuration Release -f net10.0-ios # For iOS
     dotnet build --configuration Release -f net10.0-windows10.0.19041.0 -p:Platform=x64  # For Windows
     dotnet build --configuration Release -f net10.0-maccatalyst # For macOS
     ```

### Additional Notes

- For detailed guidance on setting up your development environment, refer to the official [Microsoft .NET MAUI documentation](https://docs.microsoft.com/en-us/dotnet/maui/).
- If you encounter any issues, please check the [issues](https://github.com/ixian-platform/Spixi/issues) section on the repository for existing solutions or open a new issue.
- **For iOS Development:**
  - You need a Mac to build and run the application on iOS.
  - Follow the .NET MAUI documentation to set up your Mac environment. This includes enabling remote access, installing Xcode, and setting up Xcode command line tools.
  - Detailed instructions can be found in the [official .NET MAUI documentation for iOS setup](https://learn.microsoft.com/en-us/dotnet/maui/ios/pair-to-mac?view=net-maui-8.0).

---

## 🌱 Development Branches

* **master** - Stable, production-ready releases
* **development** - Active development, may contain unfinished features

For reproducible builds, always use the latest **release tag** on `master`.

---

## 🤝 Contributing

We welcome contributions from developers, integrators, and builders.

1. Fork this repository
2. Create a feature branch (`feature/my-change`)
3. Commit with descriptive messages
4. Open a Pull Request against `development`

Join the community on **[Discord](https://discord.gg/pdJNVhv)**.

---

## 🌍 Community & Links

* **Spixi Website**: [www.spixi.io](https://www.spixi.io)
* **Ixian Website**: [www.ixian.io](https://www.ixian.io)
* **Docs**: [docs.ixian.io](https://docs.ixian.io)
* **Discord**: [discord.gg/pdJNVhv](https://discord.gg/pdJNVhv)
* **Telegram**: [t.me/ixian\_official\_ENG](https://t.me/ixian_official_ENG)
* **Bitcointalk**: [Forum Thread](https://bitcointalk.org/index.php?topic=4631942.0)
* **GitHub**: [ixian-platform](https://www.github.com/ixian-platform)

---

## 📜 License

Licensed under the [MIT License](LICENSE).
