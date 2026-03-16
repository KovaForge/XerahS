# VEIP0001 — ShareX Video Editor 

> **Description:** A lightweight, cross-platform video editor module for post-capture workflows like trimming, cropping, and format conversion. Designed to be consumed by host applications like ShareX and XerahS utilizing a Hybrid Web/Native architecture.

**Status**: DRAFT
**Priority**: High
**Related**: 
**Repository**: [https://github.com/ShareX/ShareX.VideoEditor.git](https://github.com/ShareX/ShareX.VideoEditor.git)

---

## 1. Executive Summary
The Video Editor is a standalone, cross-platform library (`ShareX.VideoEditor`) designed to provide a lightweight, high-performance, and beautifully crafted environment for quick video edits. It focuses on post-capture workflows such as trimming, cropping, and format conversion. Operating as an independent module, it will invoke a modeless window using a Hybrid Web/Native architecture (e.g., Photino.NET or Avalonia.WebView hosting a React UI) to guarantee flawless video playback across all OS platforms while offloading intensive processing to FFmpeg.

---

## 2. Motivation
Users frequently record screencasts and need a frictionless way to trim out dead time, crop to a specific region, or convert the recording to a different format (like an optimized GIF or WebP) before sharing. Currently, users have to rely on complex, heavy third-party video editors for these trivial tasks. By creating `ShareX.VideoEditor`, we deliver a tailored, user-friendly experience specifically designed for the screencasting workflow. Early prototypes revealed that native C# media players (like Avalonia's `MediaPlayer`) lack stable cross-platform codec support (especially for MP4s on Linux/Windows). To eliminate playback bugs and deliver a best-in-class UI, we are adopting a Hybrid Web UI strategy.

---

## 3. Scope and Requirements

### 3.1 In Scope
- **Standalone Module**: Must be a fully independent `ShareX.VideoEditor` class library project, hosted at `https://github.com/ShareX/ShareX.VideoEditor.git`. Both XerahS and ShareX will integrate it. Host applications will invoke its Window directly, passing the target video file path.
- **Hybrid Architecture**: Usage of OS-native WebViews (WebView2, WebKit) to host a modern web-based UI (e.g., React/TypeScript) for flawless HTML5 video playback and premium UI rendering.
- **Trimming**: Cut out sections from the start, middle (split), or end of the video.
- **Cropping**: Visually crop the video frame to a specific dimension.
- **Format Conversion**: Convert between MP4, WebM, GIF, and WebP.
- **Annotation & Watermarks**: Incorporate simple text annotations and reuse existing watermark components/configurations provided by the host application.
- **Optimization**: Adjust framerates or resolution to achieve target file sizes before uploading.
- **Best-in-Class UI/UX**: Premium, intentionally crafted visual aesthetic conforming with the project's frontend design skills, utilizing modern web frameworks.
- **Free Components Only**: All frameworks and third-party libraries used must be 100% free and open-source.

### 3.2 Out of Scope
- Advanced multi-track timeline editing.
- Complex visual effects, audio mixing, or 3D transitions.
- Authoring video from scratch (it is strictly an editor for existing media).
- Downloading or managing FFmpeg binaries (host applications are responsible for providing this path).
- Bundling large Web frameworks like Electron (must use lightweight OS-native WebViews).
- In-browser FFmpeg processing via WebAssembly (e.g., `ffmpeg.wasm`). All export operations MUST be performed natively by the C# backend.

---

## 4. Proposed Architecture

### 4.1 UI Framework (Hybrid Web/Native)
The application will be built using a Hybrid architecture taking advantage of OS-native WebViews (e.g., via **Photino.NET** or **Avalonia.WebView** wrappers) to achieve true cross-platform functionality.
- **Frontend Stack**: React (or Vue) with TypeScript and Tailwind CSS, bundled via Vite.
- **Communication Bridge**: JSON-based message passing between the Web UI and the C# Native Host. The UI calculates trims/crops and sends configuration payloads back to C#.
- **Cost**: Only use free, open-source components. No paid controls.

### 4.2 Media Engine Pipeline
We will use a decoupled approach isolating UI playback from destructive processing:
1. **Playback/Preview (Web UI)**: 
   - Utilize standard HTML5 `<video>` tags backed by the OS-native WebView engine. This guarantees maximum compatibility and stability for MP4 and WebM files without relying on unstable native Avalonia wrappers.
2. **Processing/Rendering (C# Backend)**:
   - **Strict Requirement**: The Web UI must *only* calculate the trim timestamps, crop coordinates, and watermark text. It then passes a JSON payload back to the C# wrapper.
   - The C# wrapper receives this JSON payload and executes the native `FFmpeg.exe` process for maximum performance. This is the **only approved way** to handle destructive editing, clipping, and format conversion.
   - Use FFmpeg to asynchronously generate frame thumbnails to serve back to the Web UI for the scrubber timeline.
   - **Important**: This DLL expects the host application (e.g., ShareX, XerahS) to locate and supply the path to the FFmpeg executable.

### 4.3 Host Application Integration
- **Entry point**: Host application instantiates the `VideoEditorWindow` wrapper from the DLL, passing an options object: e.g., `new VideoEditorOptions { VideoPath = "...", FFmpegPath = "...", Theme = "...", Culture = "...", WatermarkSettings = ... }`.
- **Inheritance**: The Video Editor actively inherits translation localizations and themes, passing them down into the React UI as configuration props or CSS variables.
- **Completion**: The React UI signals the C# host to perform the FFmpeg export, which then provides C# events/Callbacks to notify the host application upon successful completion.

---

## 5. UI/UX and Aesthetic Requirements

The UI must not feel like a generic desktop window. It must be bold, striking, and meticulously aligned. Leveraging a Web UI makes this goal highly achievable.

### 5.1 Design Direction
- **Theme & Localization**: Must seamlessly ingest and apply the host app's (XerahS/ShareX) current theme and translation locale. UI defaults to Premium Dark Mode if none is provided.
- **Web Aesthetics**: Utilize modern styling systems (e.g., Tailwind CSS, Framer Motion) for glassy panels, smooth micro-animations, and interactive feedback mapping to hover/pressed/focused/disabled states. Transition states must be smooth.
- **Typography**: Utilize modern web typography (e.g., Inter, Roboto) for UI labels and bold characterful display fonts for headers.
- **Density**: Purposeful whitespace; do not cramp the UI. The interface should feel spacious but focused.

### 5.2 Window Structure & Controls
- **Modality**: Operates as a *modeless* window, allowing users to interact with XerahS while the editor remains open.
- **Main View**: Split into a prominent HTML5 video player (center stage) and a tools/timeline section (bottom).
- **Timeline Scrubber**: A custom React component that uses generated frame thumbnails along a track with draggable trim handles.
- **Context Menus**: Native-feeling web dropdowns overriding the default browser right-click menu.
- **Actions**: A prominent, visually distinct Primary Action button (e.g., "Export" or "Save") overriding standard secondary options.

---

## 6. Implementation Plan

### Phase 0: AI Agent Initialization & Cloning Instructions
As the implementing AI Agent, your first task is to scaffold the Hybrid architecture by cloning an existing open-source React trimmer foundation and porting its components into the `ShareX.VideoEditor` submodule. Execute the following steps precisely:

1. **Navigate**: Ensure your terminal is inside the root of the `ShareX.VideoEditor` submodule (e.g., `cd path\to\ShareX.VideoEditor`).
2. **Git Commit Strategy**: **CRITICAL INSTRUCTION:** As you execute this plan (and all future phases), you MUST make frequent, granular `git commit`s after any logical step or milestone (e.g., after scaffolding the UI, after stripping WebAssembly, after creating the C# project, etc.) to ensure steady progress tracking.
3. **Clone the React Foundation**: Clone a suitable open-source React video editor base (such as `limistah/react-video-trimmer` or `chemistwang/ffmpeg-video-cropper`) into a temporary directory. Example:
   ```powershell
   git clone https://github.com/limistah/react-video-trimmer.git ../temp-ui-base
   ```
4. **Port to WebUI**: Create a `src\WebUI` directory inside `ShareX.VideoEditor`. Initialize a fresh Vite+React project there (`npx create-vite . --template react-ts` or similar). Copy over the relevant timeline, player, and scrubber component files (`.tsx`/`.css`) from `temp-ui-base` to your new `src\WebUI` directory. Make a commit.
5. **Purge WebAssembly**: Aggressively search the cloned UI components for `@ffmpeg/ffmpeg` or `ffmpeg.wasm` imports and **delete them entirely**. The React UI must *only* contain the frontend visual state and send JSON payloads. Make a commit.
6. **C# Backend Scaffold**: In the root of the submodule, create the C# project (`dotnet new classlib -n ShareX.VideoEditor`). Install `Photino.NET` (`dotnet add package Photino.NET`). Make a commit.
7. **Cleanup**: Delete the temporary cloned repository.
   ```powershell
   Remove-Item -Recurse -Force ../temp-ui-base
   ```
   Make a commit.

### Phase 1: Application Skeleton & Web Bridge
- Bind the C# `PhotinoWindow` (or Avalonia equivalent) to load the compiled `index.html` produced by the `WebUI` Vite project.
- Establish the two-way interop messaging system (e.g., `window.external.sendMessage` in JS and `PhotinoWindow.RegisterWebMessageReceivedHandler` in C#) to pass configuration overrides and export payloads between C# and JavaScript.

### Phase 2: Media Playback Integration
- Build the core React components: HTML5 Video Player, Play/Pause controls, and playback state hooks.
- Ensure the C# backend can serve the local video file securely to the WebView (e.g., via a custom scheme handler or local file server).

### Phase 3: Timeline & Editing Tools UI
- Implement the custom Timeline scrubber React component with interactive trim handles.
- Integrate an FFmpeg thumbnail extractor in C# to asynchronously extract frames, encoding them to Base64 to pipe to the React timeline track.
- Build floating/docked tool panels in React for Cropping (viewport overlay), Watermarking, and Export Settings (FPS, Resolution, Format).

### Phase 4: FFmpeg Render Pipeline
- Create a `VideoExportService` in C# that receives the final payload from the React UI (trim points, crop coordinates, format).
- Execute the FFmpeg process asynchronously using the executable path supplied by the host, capturing standard output to report granular progress via the Web Bridge back to an HTML progress bar.

### Phase 5: Polish and Integration
- Final UX pass: Validate CSS animations, keyboard navigation, accessible names, focus order, and contrast compliance.
- Update XerahS and ShareX development branches to pass the proper configuration, instantiate the Hybrid window, and await the editor window to close to verify post-capture workflows.

---

## 7. Open Questions / Unknowns
- **WebView Distribution Backends**: While Windows 11 includes WebView2 by default, Windows 10 might require users to have the WebView2 runtime installed. We must ensure XerahS/ShareX bootstrapping adequately warns or side-loads the runtime if missing. Linux relies on `webkit2gtk`, which must be noted as a known dependency.

---

## 8. Rollout Strategy
1. Release a beta version of the Video Editor DLL bundled with the next XerahS/ShareX snapshot.
2. Integrate as an optional post-capture action in the ShareX/XerahS configurations.
3. Invite power users to test specific stress cases (4K videos, 60fps duration, large file optimizations).
4. Gather feedback primarily on the WebView performance, timeline smoothness, and FFmpeg export speeds.
5. Graduate to wide release, standardizing it as the default screencast handler for the ecosystem.
