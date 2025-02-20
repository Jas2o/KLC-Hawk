# KLC-Hawk 
Hawk is a research tool written in C# for seeing what happens between Kaseya Live Connect and Kaseya.AdminEndpoint.exe as a man-in-the-middle (MITM), the knowledge was used to create KLC-Finch. It was functional up to VSA 9.5.20 however will not receive any further VSA testing/development.

Hawk was also a concept to deal with issues Live Connect had such as before it had the paste clipboard feature or would leak clipboard to endpoints of closed remote control sessions. However because Finch performed significantly better than Live Connect for remote support, Hawk mostly stayed a research tool.

## Setup
- Duplicate "Kaseya Live Connect" folder to "Kaseya Live Connect-MITM".
- Rename "Kaseya.AdminEndpoint.exe" to "Kaseya.AdminEndpoint.org.exe".
- Copy Hawk into the folder or use mklink (some hints in App.xaml.cs comments), so that "Kaseya.AdminEndpoint.exe" is actually Hawk.

## Usage
- Run KLC-Hawk and start capture.
- Set KLC-Proxy to use Hawk MITM.
- From VSA, launch a connection to an agent.
  - KLC-Proxy would get the launch request, and send it to the duplicate Live Connect that has Hawk in it.
  - Live Connect will run Kaseya.AdminEndpoint.exe which runs another Hawk and tells the first Hawk about the connection request.
  - The first Hawk will run Kaseya.AdminEndpoint.org.exe and perform the MITM work.
- You can save the capture and then review it in Hawk later.
  - Some captured messages can be replayed using KLC-Lanner with KLC-Canary.

![Screenshot of KLC-Hawk](/Resources/KLC-Hawk-Capture.png?raw=true)

## Required other repos to build
- LibKaseya
- LibKaseyaLiveConnect
- VP8.NET (modified)

## Required packages to build
- Fleck
- Newtonsoft.Json
- nucs.JsonSettings
- RestSharp
- WatsonWebsocket
