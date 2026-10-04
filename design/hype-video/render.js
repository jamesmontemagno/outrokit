// Renders hype-video.html. See README.md in this folder.
//   node render.js                  -> outrokit-hype.mp4
//   node render.js gif              -> outrokit-hype.gif, made from the MP4
//   node render.js stills 3 17.4    -> still-3.png and still-17.4.png, to check a moment without a full render
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { spawn } = require('child_process');
const puppeteer = require('puppeteer-core');

const FPS = 30;
const WIDTH = 1280;
const HEIGHT = 720;
const SCALE = 1.5; // 1280x720 drawn at 1.5x gives a 1920x1080 video
const FFMPEG = process.env.FFMPEG_PATH || 'ffmpeg';
const PAGE = path.join(__dirname, 'hype-video.html');
const VIDEO = path.join(__dirname, 'outrokit-hype.mp4');
const GIF = path.join(__dirname, 'outrokit-hype.gif');

function findBrowser() {
  const candidates = [
    process.env.BROWSER_PATH,
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge',
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser',
    '/usr/bin/microsoft-edge',
  ];
  const found = candidates.find(candidate => candidate && fs.existsSync(candidate));
  if (!found) {
    throw new Error('Could not find Chrome or Edge. Set BROWSER_PATH to the browser executable.');
  }
  return found;
}

function run(command, args, input = 'ignore') {
  const child = spawn(command, args, { stdio: [input, 'inherit', 'inherit'] });
  const finished = new Promise((resolve, reject) => {
    child.on('error', error => reject(
      error.code === 'ENOENT'
        ? new Error(`Could not run ${command}. Install ffmpeg, or set FFMPEG_PATH to its executable.`)
        : error));
    child.on('close', code => (code === 0 ? resolve() : reject(new Error(`${command} exited with code ${code}`))));
  });
  return { child, finished };
}

async function makeGif() {
  if (!fs.existsSync(VIDEO)) {
    throw new Error('Render the video first with: npm run render');
  }
  // A small fixed palette suits the flat colors and keeps the file near 1.5 MB.
  const filter = 'fps=12,scale=800:-1:flags=lanczos,split[a][b];'
    + '[a]palettegen=max_colors=64:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle';
  await run(FFMPEG, ['-y', '-loglevel', 'error', '-i', VIDEO, '-vf', filter, GIF]).finished;
  console.log(GIF);
}

async function withPage(work) {
  const browser = await puppeteer.launch({
    executablePath: findBrowser(),
    headless: true,
    args: ['--hide-scrollbars', '--force-color-profile=srgb'],
  });
  try {
    const page = await browser.newPage();
    await page.setViewport({ width: WIDTH, height: HEIGHT, deviceScaleFactor: SCALE });
    await page.goto(pathToFileURL(PAGE).href);
    await page.evaluate(() => document.fonts.ready);
    await work(page);
  } finally {
    await browser.close();
  }
}

async function makeStills(times) {
  if (times.length === 0 || times.some(time => Number.isNaN(parseFloat(time)))) {
    throw new Error('Give one or more times in seconds, for example: npm run stills -- 3 17.4');
  }
  await withPage(async page => {
    for (const time of times) {
      const file = path.join(__dirname, `still-${time}.png`);
      await page.evaluate(t => window.seek(t), parseFloat(time));
      await page.screenshot({ path: file });
      console.log(file);
    }
  });
}

async function makeVideo() {
  await withPage(async page => {
    const duration = await page.evaluate(() => parseFloat(document.body.dataset.duration));
    if (!(duration > 0)) {
      throw new Error('Set the length in seconds as data-duration on <body> in hype-video.html.');
    }
    const ffmpeg = run(FFMPEG, [
      '-y', '-loglevel', 'error',
      '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'png', '-i', '-',
      '-c:v', 'libx264', '-preset', 'slow', '-crf', '15', '-pix_fmt', 'yuv420p',
      '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709',
      '-movflags', '+faststart', VIDEO,
    ], 'pipe');
    const stdin = ffmpeg.child.stdin;
    let stopped = false;
    ffmpeg.finished.then(() => { stopped = true; }, () => { stopped = true; });
    stdin.on('error', () => {}); // a failed ffmpeg is reported when `finished` is awaited below

    const frames = Math.round(FPS * duration);
    for (let frame = 0; frame < frames && !stopped; frame++) {
      await page.evaluate(t => window.seek(t), frame / FPS);
      const png = await page.screenshot({ type: 'png', optimizeForSpeed: true });
      if (!stdin.write(png)) {
        await Promise.race([new Promise(resolve => stdin.once('drain', resolve)), ffmpeg.finished.catch(() => {})]);
      }
    }
    stdin.end();
    await ffmpeg.finished;
  });
  console.log(VIDEO);
}

const [mode, ...rest] = process.argv.slice(2);
const job = mode === 'gif' ? makeGif() : mode === 'stills' ? makeStills(rest) : makeVideo();
job.catch(error => {
  console.error(error.message);
  process.exit(1);
});
