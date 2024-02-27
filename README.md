# From Letterboards to Holograms: Advancing Assistive Technology for Nonspeaking Autistic Individuals with the HoloBoard.

![HoloBoard banner](/Banner.jpg "HoloBoard")

Welcome to the accompanying repository for the ACM Digital Library Paper (link pending). You can find the Tableau page for accompanying data set [HERE](https://public.tableau.com/app/profile/lorans.alabood/viz/Dashboardproject_17085671823810/HoloBoard2022-2023).

This repository contains a Unity 2021 package designed to be used with MRTK2, Unity Netcode for GameObjects, and Microsoft Azure Spatial Anchors that allows for quickly and easily synchronizing and sharing a real-time multi-user experience for Augmented Reality devices.

## Getting Started

<ol>
    <li> Create a Unity 2021.3.4f or later project. </li>
    <li> Install the dependencies: </li>
    <ul>
        <li> https://github.com/microsoft/MixedRealityToolkit-Unity </li>
        <li> https://docs-multiplayer.unity3d.com/netcode/current/installation/index.html </li>
        <li> https://azure.microsoft.com/en-ca/products/spatial-anchors (requires a Microsoft Azure Account to use) </li>
    </ul>
    <li> Clone this repository to your local machine. </li>
    <li> Copy the C# scripts into your local Unity project. </li>
</ol>

## Usage

<ol>
    <li> Attach the SimpleShare script to a permanent object in your scene. </li>
    <li> Attach the SyncScript to any object you want scynchronized in space. </li>
</ol>

Any GameObject in the scene with an attached SyncScript will automatically be synchronized using a shared coordinate system between devices in real-time.