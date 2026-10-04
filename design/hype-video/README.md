# OutroKit hype video

The source for the short video that announces a release: a web page that plays the animation, and a script that turns it into an MP4. It uses the site's fonts and colors, so it matches [outrokit.com](https://outrokit.com/) and the share image.

The current video is 41.5 seconds, 1920×1080, 30 frames per second, with no sound. It covers the rename to OutroKit, what the app writes, transcription, title styles, burning captions, and the smaller download.

## What you need

- [Node.js](https://nodejs.org/) 18 or later
- Google Chrome or Microsoft Edge
- [ffmpeg](https://ffmpeg.org/download.html) on your `PATH`. Any standard build works; this does not need the libass build that burning captions does

## Make the video

```bash
cd design/hype-video
npm install
npm run render
```

This writes `outrokit-hype.mp4` in this folder. It takes about a minute and a half.

For a GIF of the same video, to use where a video cannot be embedded:

```bash
npm run gif
```

This writes `outrokit-hype.gif` (800 pixels wide, about 1.6 MB) from the MP4, so run `npm run render` first.

The MP4, the GIF, and any still images are ignored by git. Attach the video where you post it instead of committing it.

If the script cannot find your browser or ffmpeg, tell it where they are:

```bash
BROWSER_PATH="/path/to/chrome" FFMPEG_PATH="/path/to/ffmpeg" npm run render
```

## Check your changes without a full render

Open `hype-video.html` in Chrome or Edge and add one of these to the end of the address:

- `?play` plays the video in a loop
- `?t=17` shows the moment 17 seconds in
- `?play&t=17` plays from 17 seconds

The page is a fixed 1280×720, so make the window at least that big.

To save exact frames as images, give the times in seconds:

```bash
npm run stills -- 3 17.4 24.2
```

This writes `still-3.png`, `still-17.4.png`, and `still-24.2.png`. They are the same frames the video gets, so use them to check layout and text before rendering.

## How it works

`render.js` opens `hype-video.html` in a hidden browser, asks the page to show each frame in turn by calling `window.seek(seconds)`, takes a screenshot, and hands the screenshots to ffmpeg. Nothing plays in real time, so the result is the same on a slow or a fast computer.

For that to work, everything that moves must be driven by `seek`. It sets the position of every CSS animation on the page and updates the text that types itself. A CSS transition, a `setTimeout`, or a video element would not be captured.

### Scenes

Each `<section class="scene">` is one scene. Its times are in seconds from the start of the video:

```html
<section class="scene" style="--t0: 14.6s; --t1: 20.3s" data-t0="14.6">
```

- `--t0` is when the scene starts and `--t1` is when it ends. A scene fades out at `--t1`, and the next one fades in just after.
- `data-t0` repeats the start time for the script. Keep it the same as `--t0`.
- `data-duration` on `<body>` is the length of the whole video. The last scene has `--t1: 999s` so it stays until the end.

### Things inside a scene

Every time below is in seconds **from the start of its scene**, so a scene can be moved without retiming its contents.

| To do this | Write this |
| --- | --- |
| Make something appear | `class="a" style="--at: 0.5s"` |
| Choose how it appears | Add `--an: wipe`, `pop`, or `press` (the default slides up) and `--d: 0.4s` for how long it takes |
| Type text one character at a time | `data-type data-at="1.2"`, with `data-cps="46"` for the speed in characters per second. Add `data-hold` to keep the cursor after it finishes |
| Count a number up or down | `data-count data-from="116" data-to="11" data-at="0.9" data-dur="1.1" data-suffix=" MB"`. Add `data-ease="linear"` for a steady count |
| Show something for part of a scene | `data-from="1.2" data-to="2.4"` |
| Highlight something for part of a scene | `data-on-from="0.6" data-on-to="1.9"`, which adds the class `on` during that time |

Your own CSS animations work too. Give them a delay based on the scene start, such as `animation-delay: calc(var(--t0) + 0.9s)`.

## Updating it for a new release

1. Change what is specific to the release: the `New in 1.5` stamp, the download sizes in the "smaller download" scene, and any scene about a feature that is no longer news.
2. To add a scene, copy the one closest to what you want, change its text, and give it a start and end time.
3. Move every later scene by the same amount: `--t0`, `--t1`, and `data-t0` on each. Then update `data-duration` on `<body>`.
4. Check the new scene and the cuts either side of it with `?play` or `npm run stills`.
5. Run `npm run render` and watch the result before posting it.

Keep to the rules the site follows, from [`DESIGN.md`](../../DESIGN.md) and [`PRODUCT.md`](../../PRODUCT.md):

- The example episode is made up. Keep the `Example episode` stamp wherever its details are shown, and do not present it as a real episode or real output.
- Numbers such as download sizes must be real. Take them from the release.
- Use only the four colors: blue, card white, yellow, and red.
- The typewriter font is for what the app writes and what someone types. Headings and sentences use Archivo.
