(function () {
  'use strict';

  const root = document.documentElement;
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* 配色切换：header 里的内联脚本已经定好初始值，这里负责手动切换和跟随系统 */
  const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');
  const themeColors = {};
  document.querySelectorAll('meta[data-theme-color]').forEach((meta) => {
    themeColors[meta.dataset.themeColor] = meta.getAttribute('content');
  });

  const MODES = ['auto', 'light', 'dark'];
  const MODE_LABELS = { auto: '跟随系统', light: '浅色', dark: '深色' };

  const applyMode = (mode) => {
    const resolved = mode === 'auto' ? (darkQuery.matches ? 'dark' : 'light') : mode;
    root.dataset.themeMode = mode;
    root.dataset.theme = resolved;

    // 手动指定配色时，地址栏底色不能再跟着系统走
    document.querySelectorAll('meta[data-theme-color]').forEach((meta) => {
      meta.setAttribute('content', mode === 'auto' ? themeColors[meta.dataset.themeColor] : themeColors[resolved]);
    });
  };

  const current = () => (MODES.includes(root.dataset.themeMode) ? root.dataset.themeMode : 'auto');
  applyMode(current());

  const toggle = document.querySelector('.theme-toggle');
  if (toggle) {
    const label = (mode) => {
      const next = MODES[(MODES.indexOf(mode) + 1) % MODES.length];
      toggle.setAttribute('aria-label', `配色：${MODE_LABELS[mode]}，点击切换到${MODE_LABELS[next]}`);
      toggle.setAttribute('title', toggle.getAttribute('aria-label'));
    };

    label(current());

    toggle.addEventListener('click', () => {
      const next = MODES[(MODES.indexOf(current()) + 1) % MODES.length];
      applyMode(next);
      label(next);
      try { localStorage.setItem('cc-color-mode', next); } catch (e) {}
    });
  }

  // 跟随系统时，系统换了配色页面立刻跟上，不用刷新
  const onSchemeChange = () => {
    if (root.dataset.themeMode !== 'light' && root.dataset.themeMode !== 'dark') applyMode('auto');
  };
  if (darkQuery.addEventListener) {
    darkQuery.addEventListener('change', onSchemeChange);
  } else if (darkQuery.addListener) {
    darkQuery.addListener(onSchemeChange);
  }

  /* 导航栏滚动后加毛玻璃底 */
  const nav = document.getElementById('site-nav');
  const toTop = document.querySelector('.to-top');
  if (nav || toTop) {
    // 只读 scrollY，不触发重排，所以直接在滚动回调里做，不用 rAF 排队
    const onScroll = () => {
      const y = window.scrollY;
      if (nav) nav.classList.toggle('is-scrolled', y > 24);
      if (toTop) toTop.hidden = y < 600;
    };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  }

  if (toTop) {
    toTop.addEventListener('click', () => {
      window.scrollTo({ top: 0, behavior: reduceMotion ? 'auto' : 'smooth' });
    });
  }

/* 通用提示弹窗 */
  const modal = document.getElementById('feature-modal');
  if (!modal) return;

  const title = modal.querySelector('#modal-title');
  const message = modal.querySelector('#modal-message');
  const eyebrow = modal.querySelector('#modal-eyebrow');
  const iconUse = modal.querySelector('#modal-icon use');
  const okay = modal.querySelector('.modal-okay');
  let lastFocused = null;

  const close = () => {
    modal.hidden = true;
    document.body.classList.remove('modal-open');
    if (lastFocused) lastFocused.focus();
  };

  const show = (options) => {
    lastFocused = document.activeElement;
    title.textContent = options.title || '温馨提示';
    message.textContent = options.message || '';
    eyebrow.textContent = options.eyebrow || 'OUR STORY';
    if (iconUse) iconUse.setAttribute('href', '#i-' + (options.icon || 'heart'));
    okay.textContent = options.button || '知道啦';
    modal.hidden = false;
    document.body.classList.add('modal-open');
    okay.focus();
  };

  window.ccShowModal = show;

  document.querySelectorAll('[data-feature]').forEach((card) => card.addEventListener('click', () => show({
    title: card.dataset.feature,
    message: '这个入口会在后续功能中逐步开放',
    eyebrow: 'UI PREVIEW',
    icon: card.dataset.featureIcon || 'sparkles'
  })));

  modal.querySelector('.modal-close').addEventListener('click', close);
  okay.addEventListener('click', close);
  modal.addEventListener('click', (event) => { if (event.target === modal) close(); });
  document.addEventListener('keydown', (event) => { if (event.key === 'Escape' && !modal.hidden) close(); });

  /* 封面：图到了再淡入，没到之前由骨架屏占着位置。
     脚本跑起来时图可能已经在缓存里，这种情况 load 不会再触发，得先问一句 complete */
  document.querySelectorAll('[data-cover]').forEach((cover) => {
    const image = cover.querySelector('img');
    if (!image) {
      // 没有封面的那种占位格子，别让骨架一直扫下去
      cover.classList.add('is-ready');
      return;
    }

    const mark = (state) => cover.classList.add(state);

    if (image.complete) {
      mark(image.naturalWidth > 0 ? 'is-ready' : 'is-failed');
      return;
    }

    image.addEventListener('load', () => mark('is-ready'));
    image.addEventListener('error', () => mark('is-failed'));
  });

/* 进入视口时淡入。内容默认是可见的，.will-reveal 由 JS 补上，
     所以没有 JS 时不受影响；再加一个兜底定时器，万一观察器不回调也不会留下空白。 */
  const revealTargets = document.querySelectorAll('.feature-card, .moment-entry, .recent-card, .post-card');
  if (revealTargets.length && !reduceMotion && 'IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, { rootMargin: '0px 0px -8% 0px' });

    revealTargets.forEach((el, index) => {
      el.classList.add('will-reveal');
      el.style.setProperty('--reveal-delay', Math.min(index % 6, 5) * 55 + 'ms');
      observer.observe(el);
    });

    window.setTimeout(() => {
      revealTargets.forEach((el) => el.classList.add('is-visible'));
      observer.disconnect();
    }, 2500);
  }
}());
