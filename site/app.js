(() => {
  const root = document.documentElement;
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ── The cassette turns while the card is being typed ── */

  const cassette = document.getElementById('cassette');
  let typingCount = 0;

  const setPlaying = (on) => {
    typingCount = Math.max(0, typingCount + (on ? 1 : -1));
    if (cassette) cassette.classList.toggle('is-playing', typingCount > 0);
  };

  const prepare = (el) => {
    if (el.classList.contains('is-prepped')) return;
    const text = el.textContent;
    const typed = document.createElement('span');
    const untyped = document.createElement('span');
    typed.className = 'typed';
    untyped.className = 'untyped';
    untyped.textContent = text;
    el.textContent = '';
    el.append(typed, untyped);
    el.dataset.full = text;
    el.classList.add('is-prepped');
  };

  const type = (el, charsPerSecond) =>
    new Promise((resolve) => {
      const full = el.dataset.full;
      const [typed, untyped] = el.children;
      const run = (el.typeRun = (el.typeRun || 0) + 1);
      let start;

      el.classList.add('is-typing');

      const frame = (now) => {
        if (el.typeRun !== run) return resolve();
        if (start === undefined) start = now;
        const count = Math.min(full.length, Math.ceil(((now - start) / 1000) * charsPerSecond));
        typed.textContent = full.slice(0, count);
        untyped.textContent = full.slice(count);
        if (count < full.length) return requestAnimationFrame(frame);
        el.classList.remove('is-typing');
        resolve();
      };

      requestAnimationFrame(frame);
    });

  const typeTargets = [...document.querySelectorAll('[data-type]')];

  const typeCard = async () => {
    clearTimeout(window.__typeGuard);
    typeTargets.forEach(prepare);
    root.classList.remove('will-type');
    setPlaying(true);
    for (const el of typeTargets) {
      await type(el, 190);
    }
    setPlaying(false);
  };

  if (!reduceMotion && typeTargets.length) {
    const fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
    Promise.race([fontsReady, new Promise((r) => setTimeout(r, 800))]).then(typeCard);
  }

  /* ── Title style tick boxes retype the example title ── */

  const exampleTitles = {
    balanced: 'A Month in the Closet: What Actually Fixed Our Echo',
    descriptive: 'Recording a Podcast in a Closet for One Month',
    curiosity: 'The $12 Fix That Beat Our Acoustic Panels',
    question: 'Do You Really Need Acoustic Panels to Sound Good?',
    howto: 'How to Cut Room Echo Without Buying Any Gear',
    playful: 'Hanging Up on Echo: Our Month Among the Coats',
    professional: 'Practical Room Treatment for Independent Podcast Producers',
    seo: 'Podcast Echo Fix: Closet Recording, Blankets, and Mic Placement',
    mixed: 'Is Your Room the Problem? A Closet Recording Experiment',
  };

  const titleEl = document.getElementById('demo-title');
  const titleLive = document.getElementById('title-live');

  document.querySelectorAll('input[name="style"]').forEach((radio) => {
    radio.addEventListener('change', async () => {
      const title = exampleTitles[radio.value];
      if (!title || !titleEl) return;

      const styleName = radio.nextElementSibling ? radio.nextElementSibling.textContent : radio.value;
      if (titleLive) titleLive.textContent = `Example title, ${styleName} style: ${title}`;

      if (reduceMotion) {
        titleEl.textContent = title;
        return;
      }

      prepare(titleEl);
      titleEl.dataset.full = title;
      titleEl.children[0].textContent = '';
      titleEl.children[1].textContent = title;
      setPlaying(true);
      await type(titleEl, 70);
      setPlaying(false);
    });
  });

  /* ── Operating system switch ── */

  const systems = ['windows', 'macos', 'linux'];
  const osRadios = [...document.querySelectorAll('input[name="os"]')];

  const detectSystem = () => {
    const platform = ((navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || '').toLowerCase();
    const agent = navigator.userAgent.toLowerCase();
    if (platform.includes('mac') || /iphone|ipad/.test(agent)) return 'macos';
    if (platform.includes('win')) return 'windows';
    if (platform.includes('linux') || /android|cros/.test(agent)) return 'linux';
    return 'windows';
  };

  const readSystem = () => {
    try {
      return localStorage.getItem('pmg-os');
    } catch {
      return null;
    }
  };

  const applySystem = (system) => {
    root.dataset.os = system;
    osRadios.forEach((radio) => {
      radio.checked = radio.value === system;
    });
  };

  const saved = readSystem();
  applySystem(systems.includes(saved) ? saved : detectSystem());

  osRadios.forEach((radio) => {
    radio.addEventListener('change', () => {
      applySystem(radio.value);
      try {
        localStorage.setItem('pmg-os', radio.value);
      } catch {
        /* Private mode: the choice just lasts for this visit. */
      }
    });
  });

  /* ── Copy buttons ── */

  const copyStatus = document.getElementById('copy-status');

  const writeClipboard = async (text) => {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      const field = document.createElement('textarea');
      field.value = text;
      field.setAttribute('readonly', '');
      field.style.cssText = 'position:fixed;top:0;left:0;opacity:0';
      document.body.append(field);
      field.select();
      let ok = false;
      try {
        ok = document.execCommand('copy');
      } catch {
        ok = false;
      }
      field.remove();
      return ok;
    }
  };

  const selectText = (node) => {
    const range = document.createRange();
    range.selectNodeContents(node);
    const selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
  };

  const wireCopy = (button, codeEl) => {
    const text = button.dataset.copy;
    const idleName = `Copy command: ${text}`;
    button.setAttribute('aria-label', idleName);

    button.addEventListener('click', async () => {
      const ok = await writeClipboard(text);
      clearTimeout(button.resetTimer);

      button.classList.toggle('is-copied', ok);
      button.classList.toggle('is-failed', !ok);
      button.textContent = ok ? 'Copied' : 'Select it';
      button.setAttribute('aria-label', ok ? `Copied: ${text}` : `Select it: ${text}`);

      if (!ok && codeEl) selectText(codeEl);
      if (copyStatus) {
        copyStatus.textContent = ok
          ? `Copied ${text}. Paste it into your terminal and press Enter.`
          : 'Could not copy automatically. The command is selected, so copy it with your keyboard.';
      }

      button.resetTimer = setTimeout(() => {
        button.classList.remove('is-copied', 'is-failed');
        button.textContent = 'Copy';
        button.setAttribute('aria-label', idleName);
      }, 2200);
    });
  };

  document.querySelectorAll('.cmd').forEach((cmd) => {
    const codeEl = cmd.querySelector('code');
    if (!codeEl) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'copy';
    button.textContent = 'Copy';
    button.dataset.copy = codeEl.textContent;
    cmd.append(button);
    wireCopy(button, codeEl);
  });

  document.querySelectorAll('button.copy[hidden]').forEach((button) => {
    button.hidden = false;
    wireCopy(button, button.closest('.cassette')?.querySelector('.cassette__cmd'));
  });
})();
