---
name: hype-video
description: Update and render the OutroKit hype video, the short animated video that announces a release on social media. Use when asked for a hype, launch, release, promo, or announcement video for OutroKit, to add or change a scene in the existing one, to refresh it for a new version, or to render the MP4 or GIF.
---

# OutroKit hype video

OutroKit announces a release with a short silent video in the same cassette J-card style as [outrokit.com](https://outrokit.com/). The video is not a screen recording. It is a web page, `design/hype-video/hype-video.html`, that a script steps through one frame at a time and hands to ffmpeg. Your job is usually to bring that page up to date for a new release, render it, and tell the user where the file is.

Read `design/hype-video/README.md` before changing anything. It has the commands, the way scenes and timings are written, and the table of attributes for typing text, counting numbers, and showing things for part of a scene. This file covers what the README does not: what belongs in the video, where the facts come from, and what tends to go wrong.

## What the video is for

Someone scrolling a feed sees it with the sound off and gives it a few seconds. Each scene makes one point they can take in at a glance: a short headline on the left, and on the right the thing itself happening (a title typing itself, a waveform turning into subtitles, captions appearing on a picture). A scene that needs reading has too much in it.

Aim for 3 to 6 seconds a scene and under 45 seconds in total. The longest scene is the J-card filling itself in, which carries the four core outputs and is the one piece of motion the site also uses.

## Deciding what goes in

Take what is new from `src/Console/UI/WhatsNew.cs`. The first entry of `WhatsNew.Releases` is the latest release, and it is already written for users. Feature the changes someone would switch tools for, largest first, and leave out fixes and internal changes. If the user names features, those win. If it is unclear which two or three deserve a scene, ask rather than making a scene for everything.

The scenes fall into two groups:

- **Kept from release to release:** what the app writes (the J-card), starting from a recording, title styles, and the outro with the install command and the site address.
- **About one release:** the rename from Podcast Metadata Generator, the `New in 1.5` stamp on burning captions, and the download size comparison. Replace or drop these when they are no longer news, and move the stamp to whatever is new.

Every number and claim must be true of the release being announced. Take package sizes from the release assets (`gh release view v<version> --json assets`), platform support from what CI tests, and the install command from the README. If the release is not published yet, say so to the user, because the video ends on a command that will not work until it is.

## Rules the page follows

These come from `DESIGN.md` and `PRODUCT.md`, and the video is held to them like the site is:

- The example episode, "Spare Room Radio" episode 031, is made up. Wherever its titles, chapters, or subtitles appear, they are an example, and the J-card carries an `Example episode` stamp. Reuse the example text from `site/app.js` and `design/og-image.html` so the video and the site tell the same story. Do not present invented text as real output or as a real show.
- Four colors only: tape blue for the ground, card white for anything read, yellow, and red. The CSS variables at the top of the page are the palette.
- Sometype Mono is for what the app writes and what a person types. Headlines and sentences are Archivo.
- Blue is the background and content sits on a card. Do not build a scene on a dark terminal panel.

Do not describe the video as made with OutroKit, and do not add a recording of the real app unless the user asks for one. Recording the app runs real generations against the user's Copilot account.

## Making the change

The page is 1280×720 and nothing in it reflows, so layout problems are overflow problems: a headline that wraps to a third line, a typed title longer than its ruled line, a row of chips wider than its column. Size new text against the longest string it will show.

Everything that moves has to be driven by `window.seek`. CSS animations are, and so are the `data-` attributes in the README. A CSS transition, a timer, or anything that plays on its own will be frozen or missing in the render, with no error.

To add a scene, copy the existing one closest to it. The two-column `.split` card is the usual starting point. After adding or lengthening a scene, move every later scene by the same amount (`--t0`, `--t1`, and `data-t0` on each section) and update `data-duration` on `<body>`. Missing one of these is the most common mistake, and it shows up as a scene that never appears or two that play at once.

## Checking it

A render that finishes proves only that frames were written. Look at them.

1. Run `npm install` in `design/hype-video` if `node_modules` is missing. The render needs Node 18 or later, Chrome or Edge, and ffmpeg. If one is missing, tell the user what to install instead of installing system tools yourself.
2. Save stills with `npm run stills -- <times>` and view each image. Choose times that show every new or changed scene when it is fully built, one partway through anything that types or counts, and one about 0.15 seconds after each scene boundary you moved, to confirm the old scene has gone before the new one arrives.
3. Check each still for text that overflows or wraps badly, a number caught at the wrong value, and anything from the previous release that should have changed.
4. Run `npm run render`, then confirm the length with `ffprobe` and pull a few frames from the MP4 to compare with the stills. Run `npm run gif` if the user wants a GIF.

You cannot watch the video play. Say that when you report, and ask the user to watch it once before posting.

## Finishing

The MP4, GIF, and stills are ignored by git and stay out of commits. Commit the changes to `hype-video.html`, and to the README in that folder if the commands or the way scenes work changed. Delete the stills when you are done.

Tell the user where `outrokit-hype.mp4` is, how long it runs, which scenes changed, and anything you left out and why.

If the user also wants the post to go with it, keep it to what the video shows and end with the install command and `https://outrokit.com`. X allows 280 characters, counts any link as 23, and counts an emoji as 2, so count before handing it over.
