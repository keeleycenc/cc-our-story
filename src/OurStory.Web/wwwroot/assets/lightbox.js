/*
 * 图片查看器。
 *
 * 支持跟手滑动与渐进加载：滑动时预加载前后压缩图，当前图片优先展示缩略图，
 * 原图后台加载并解码后无感替换，避免闪烁、白屏和布局跳动。
 *
 * 图片尺寸通过 data-width/data-height 提前占位，防止加载过程中页面抖动。
 *
 * 通过 data-lightbox 分组、data-full 指定原图地址即可复用，
 * 支持正文图片、相册、时间轴等场景。
 *
 * 支持 window.ccLightbox.open(items, index) 手动调用。
 */
(function () {
  'use strict';

  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const SLIDE_MS = 280;          // 翻页动画时长，需与 CSS transition 保持一致
  const NEAR = 2;                // 当前图片前后预加载数量
  const MAX_SCALE = 5;           // 最大缩放倍数
  const MIN_SCALE = 1;           // 最小缩放倍数
  const AXIS_LOCK = 8;           // 超过该距离后锁定滑动方向
  const SWIPE_PART = 0.22;       // 横向滑动超过屏宽比例后触发翻页
  const SWIPE_CAP = 120;         // 最大翻页拖动距离限制
  const SWIPE_SPEED = 0.45;      // 快速滑动触发翻页的速度阈值(px/ms)
  const SPEED_WINDOW = 120;      // 速度计算时间窗口(ms)
  const SPEED_SPAN = 24;         // 最小速度采样跨度(ms)
  const CLOSE_DROP = 110;        // 下拉关闭阈值(px)
  const HINT_DELAY = 350;        // 原图读这么久还没到才提示，快的不打扰
  const RUBBER = 0.35;           // 边界拖拽阻尼系数

  let overlay = null;            // 图片查看器遮罩层，按需创建
  let parts = null;              // 常用 DOM 节点缓存
  let items = [];                // 当前图片组及加载状态
  let current = 0;               // 当前图片索引
  let scale = 1;                 // 当前缩放比例
  let offsetX = 0;               // 缩放后的水平偏移
  let offsetY = 0;               // 缩放后的垂直偏移
  let dragX = 0;                 // 当前手势水平拖动距离
  let dragY = 0;                 // 当前手势垂直拖动距离
  let stageWidth = 0;            // 图片舞台宽度
  let settleTimer = 0;           // 动画归位定时器
  let hintTimer = 0;             // 高清加载提示的延时
  let lastFocused = null;        // 打开前保存的焦点元素

  const icon = (name) => '<svg class="icon" aria-hidden="true"><use href="#i-' + name + '"></use></svg>';

  /* ---- 建结构 ---- */

  const build = () => {
    const node = document.createElement('div');
    node.className = 'lightbox';
    node.id = 'lightbox';
    node.hidden = true;
    node.setAttribute('role', 'dialog');
    node.setAttribute('aria-modal', 'true');
    node.setAttribute('aria-label', '图片查看器');
    node.innerHTML =
      '<div class="lightbox-bar">' +
      '<span class="lightbox-count" aria-live="polite"></span>' +
      '<span class="lightbox-tools">' +
      '<button type="button" class="lightbox-button" data-act="out" aria-label="缩小">' + icon('zoom-out') + '</button>' +
      '<button type="button" class="lightbox-button" data-act="in" aria-label="放大">' + icon('zoom-in') + '</button>' +
      '<button type="button" class="lightbox-button" data-act="close" aria-label="关闭查看器">' + icon('x') + '</button>' +
      '</span>' +
      '</div>' +
      '<button type="button" class="lightbox-nav is-prev" data-act="prev" aria-label="上一张">' + icon('chevron-left') + '</button>' +
      '<button type="button" class="lightbox-nav is-next" data-act="next" aria-label="下一张">' + icon('chevron-right') + '</button>' +
      '<div class="lightbox-stage" data-stage>' +
      '<div class="lightbox-track" data-track></div>' +
      '</div>' +
      '<p class="lightbox-caption" data-caption></p>' +
      '<span class="lightbox-hint" data-hint hidden aria-live="polite"><i class="lightbox-dot" aria-hidden="true"></i>高清加载中</span>';

    document.body.append(node);

    parts = {
      stage: node.querySelector('[data-stage]'),
      track: node.querySelector('[data-track]'),
      caption: node.querySelector('[data-caption]'),
      hint: node.querySelector('[data-hint]'),
      count: node.querySelector('.lightbox-count'),
      prev: node.querySelector('[data-act="prev"]'),
      next: node.querySelector('[data-act="next"]')
    };

    bind(node);
    return node;
  };

  /**
   * 按图片报出来的真实尺寸校正占位框的比例。
   *
   * 比例只要和图片对不上，object-fit: contain 就会把图放在框中间，
   * 两侧露出底色 —— 看着就像图片外面套了个框。所以谁先报出尺寸就按谁的来：
   * 事先不知道比例的（后台图片库列的是裁过的封面）要补，
   * 已知但差得明显的（拿的是别的规格）也要纠回来。
   */
  const fit = (item, width, height) => {
    if (!item || !(width > 0) || !(height > 0)) return;

    const ratio = width / height;
    if (item.ratio && Math.abs((item.fitted || 0) - ratio) < ratio * 0.02) return;

    item.ratio = width + ' / ' + height;
    item.fitted = ratio;
    item.frame.style.setProperty('--lightbox-ratio', item.ratio);
  };

  // 一张图一个格子：比例先按已知尺寸占好，压缩图垫底，原图到了盖上去
  const makeSlide = (item) => {
    const slide = document.createElement('div');
    slide.className = 'lightbox-slide';

    const frame = document.createElement('div');
    frame.className = 'lightbox-frame';
    item.frame = frame;                     // fit() 要用，得先挂上
    if (item.ratio) frame.style.setProperty('--lightbox-ratio', item.ratio);

    const preview = document.createElement('img');
    preview.className = 'lightbox-preview';
    preview.decoding = 'async';
    preview.draggable = false;
    preview.alt = '';
    preview.setAttribute('aria-hidden', 'true');

    const full = document.createElement('img');
    full.className = 'lightbox-full';
    full.decoding = 'async';
    full.draggable = false;
    full.alt = item.alt;

    const note = document.createElement('p');
    note.className = 'lightbox-note';
    note.hidden = true;
    note.innerHTML = icon('image-off') + '该图片加载失败';

    preview.addEventListener('load', () => {
      slide.classList.add('is-ready');
      fit(item, preview.naturalWidth, preview.naturalHeight);
    });

    frame.append(preview, full, note);
    slide.append(frame);

    item.slide = slide;
    item.previewNode = preview;
    item.fullNode = full;
    item.note = note;
    return slide;
  };

  /* ---- 加载：压缩图先顶上，原图后台读好再换 ---- */

  const ensurePreview = (item) => {
    if (!item || item.previewStarted || !item.previewUrl) return;

    item.previewStarted = true;
    item.previewNode.src = item.previewUrl;
  };

  const ensureFull = (item, then) => {
    if (!item) return;

    if (item.fullDone) {
      if (then) then();
      return;
    }

    // 之前当邻居预取过、这会儿轮到它了：接着等那一份，别再开一条
    if (then) item.then = then;
    if (item.loader) return;

    const loader = new Image();
    item.loader = loader;
    loader.decoding = 'async';

    const swap = () => {
      // 解码这段时间里被 drop 撤掉了（已经翻远了），那就到此为止
      if (item.loader !== loader) return;

      const next = item.then;

      item.loader = null;
      item.then = null;
      item.fullDone = true;

      // 压缩图还没到就先按原图的尺寸把框摆正，别让它顶着默认比例露底色
      fit(item, loader.naturalWidth, loader.naturalHeight);
      item.fullNode.src = item.fullUrl;
      item.slide.classList.add('is-full');

      hint();
      if (next) next();
    };

    loader.onload = () => {
      // 先在后台把它解好码再挂上去，避免边解边画，出现半张图
      if (loader.decode) loader.decode().then(swap, swap);
      else swap();
    };

    loader.onerror = () => {
      item.loader = null;
      item.then = null;
      item.slide.classList.add('is-failed');
      item.note.hidden = false;
      hint();
    };

    loader.src = item.fullUrl;
  };

  const drop = (item) => {
    if (!item || !item.loader) return;

    item.loader.onload = null;
    item.loader.onerror = null;
    item.loader.src = '';
    item.loader = null;
    item.then = null;
  };

  /*
   * 走远了的那些图从节点上摘下来。
   *
   * 一张 4000×3000 的原图解开就是几十兆，一路翻过去全留着，
   * 手机上迟早被系统收掉整个页面。摘掉之后压缩图重新露出来，
   * 翻回去时再从浏览器缓存里取，不会再走一趟网络。
   */
  const unloadFull = (item) => {
    if (!item || !item.fullDone) return;

    item.fullDone = false;
    item.fullNode.removeAttribute('src');
    item.slide.classList.remove('is-full');
  };

  const unloadPreview = (item) => {
    if (!item || !item.previewStarted) return;

    item.previewStarted = false;
    item.previewNode.removeAttribute('src');
    item.slide.classList.remove('is-ready');
  };

  /*
   * 省流量或者网络本来就慢的时候，只备压缩图，不提前拉原图。
   *
   * 原图动辄几百 KB，慢网上把左右两张也一起拉，只会跟当前这张抢带宽，
   * 手里正看的这张反而更久才清楚。压缩图小，照备不误。
   */
  const thrifty = () => {
    const link = navigator.connection;
    if (!link) return false;

    return Boolean(link.saveData) || /^(slow-)?2g$|^3g$/.test(link.effectiveType || '');
  };

  const idle = (run) => {
    if (window.requestIdleCallback) window.requestIdleCallback(run, { timeout: 1500 });
    else window.setTimeout(run, 300);
  };

  const neighbours = () => {
    if (thrifty()) return;
    ensureFull(items[current - 1]);
    ensureFull(items[current + 1]);
  };

  /**
   * 停下来之后再安排加载：滑动的那几帧不动网络，也不动 DOM。
   *
   * 当前这张读原图，前后几张先把压缩图备好；
   * 等当前这张读完了，空闲时再顺手把左右两张的原图也拉上，
   * 于是接着翻的时候多半已经是现成的高清图了。
   */
  const settle = () => {
    for (let step = -NEAR; step <= NEAR; step++) {
      ensurePreview(items[current + step]);
    }

    // 离得远的：还在读的撤掉，别占着连接；已经读进来的摘下来，别占着内存
    items.forEach((item, index) => {
      const away = Math.abs(index - current);
      if (away <= 1) return;

      drop(item);
      unloadFull(item);
      if (away > NEAR + 1) unloadPreview(item);
    });

    // 当前这张读完了，空闲时再顺手把左右两张也拉上；中途翻走了就不管了
    const item = items[current];
    ensureFull(item, () => {
      if (items[current] === item) idle(neighbours);
    });
  };

  /* ---- 位置：轨道整体位移 + 当前这张的缩放 ---- */

  const measure = () => {
    stageWidth = parts.stage.clientWidth || window.innerWidth;
  };

  const place = (animate) => {
    const x = (-current * stageWidth) + dragX;

    parts.track.style.transition = animate && !reduceMotion
      ? 'transform ' + SLIDE_MS + 'ms cubic-bezier(.22,.61,.36,1)'
      : 'none';
    parts.track.style.transform = 'translate3d(' + x + 'px,' + dragY + 'px,0)';

    // 往下拖的时候顺带把背景淡出去，松手要么关掉要么弹回来
    overlay.style.setProperty('--lightbox-dim', String(Math.max(0.3, 1 - (Math.abs(dragY) / 420))));
  };

  const clampOffset = () => {
    const item = items[current];
    if (!item) return;

    // 放大之后超出舞台的那部分，就是能拖动的范围
    const stage = parts.stage.getBoundingClientRect();
    const roomX = Math.max(0, ((item.frame.offsetWidth * scale) - stage.width) / 2);
    const roomY = Math.max(0, ((item.frame.offsetHeight * scale) - stage.height) / 2);

    offsetX = Math.min(roomX, Math.max(-roomX, offsetX));
    offsetY = Math.min(roomY, Math.max(-roomY, offsetY));
  };

  const applyZoom = (animate) => {
    const item = items[current];
    if (!item) return;

    clampOffset();
    item.frame.style.transition = animate && !reduceMotion ? 'transform .22s ease' : 'none';
    item.frame.style.transform = 'translate(' + offsetX + 'px,' + offsetY + 'px) scale(' + scale + ')';
    overlay.classList.toggle('is-zoomed', scale > 1.001);
  };

  const resetZoom = () => {
    const item = items[current];

    scale = 1;
    offsetX = 0;
    offsetY = 0;

    if (item) {
      item.frame.style.transition = 'none';
      item.frame.style.transform = '';
    }

    overlay.classList.remove('is-zoomed');
  };

  // origin 是以舞台中心为原点的坐标，围着它缩放，手指底下那一点才不会跑
  const zoomTo = (next, origin, animate) => {
    const target = Math.min(MAX_SCALE, Math.max(MIN_SCALE, next));
    if (Math.abs(target - scale) < 0.001) return;

    if (origin) {
      const ratio = target / scale;
      offsetX = origin.x - ((origin.x - offsetX) * ratio);
      offsetY = origin.y - ((origin.y - offsetY) * ratio);
    }

    scale = target;
    if (target === MIN_SCALE) {
      offsetX = 0;
      offsetY = 0;
    }

    applyZoom(animate !== false);
  };

  const stageOrigin = (clientX, clientY) => {
    const box = parts.stage.getBoundingClientRect();
    return { x: clientX - box.left - (box.width / 2), y: clientY - box.top - (box.height / 2) };
  };

  /* ---- 换图 ---- */

  /**
   * 高清加载提示。
   *
   * 压缩图这时候已经顶上了，看的人不缺东西看，只是还不够清楚，
   * 所以提示放在左下角小小一条，不搁在图中间挡着。
   * 读得快的压根来不及显示，免得闪一下。
   */
  const hint = () => {
    const item = items[current];
    const waiting = Boolean(item) && !item.fullDone && !item.slide.classList.contains('is-failed');

    window.clearTimeout(hintTimer);

    if (!waiting) {
      parts.hint.hidden = true;
      return;
    }

    hintTimer = window.setTimeout(() => {
      parts.hint.hidden = false;
    }, HINT_DELAY);
  };

  const chrome = () => {
    const item = items[current];
    const caption = item ? item.caption : '';

    parts.caption.textContent = caption;
    parts.caption.hidden = !caption;
    parts.count.textContent = items.length > 1 ? (current + 1) + ' / ' + items.length : '';
    parts.prev.hidden = items.length < 2;
    parts.next.hidden = items.length < 2;
    parts.prev.disabled = current === 0;
    parts.next.disabled = current === items.length - 1;
  };

  const goTo = (index, animate) => {
    const target = Math.min(items.length - 1, Math.max(0, index));

    if (target !== current) resetZoom();
    current = target;
    dragX = 0;
    dragY = 0;

    place(animate !== false);
    chrome();
    hint();

    // 动画跑完再安排加载，滑的那几帧就干干净净的
    window.clearTimeout(settleTimer);
    settleTimer = window.setTimeout(settle, animate === false || reduceMotion ? 0 : SLIDE_MS);
  };

  const step = (delta) => goTo(current + delta, true);

  /* ---- 开关 ---- */

  const close = () => {
    if (!overlay || overlay.hidden) return;

    window.clearTimeout(settleTimer);
    window.clearTimeout(hintTimer);
    parts.hint.hidden = true;
    overlay.hidden = true;
    overlay.classList.remove('is-swiping', 'is-dragging', 'is-zoomed');
    overlay.style.removeProperty('--lightbox-dim');
    document.body.classList.remove('modal-open');

    // 还在半路的请求一律撤掉，节点也清空，别留着一堆原图占内存
    items.forEach(drop);
    parts.track.textContent = '';
    items = [];

    if (lastFocused && document.contains(lastFocused)) lastFocused.focus();
    lastFocused = null;
  };

  const open = (list, index) => {
    const usable = (list || []).filter((item) => item && (item.full || item.preview));
    if (!usable.length) return;

    overlay = overlay || build();
    lastFocused = document.activeElement;

    items.forEach(drop);
    parts.track.textContent = '';
    items = usable.map((item) => ({
      fullUrl: item.full || item.preview,
      previewUrl: item.preview || '',
      alt: item.alt || '',
      caption: item.caption || '',
      ratio: item.ratio || ''
    }));

    const batch = document.createDocumentFragment();
    items.forEach((item) => batch.append(makeSlide(item)));
    parts.track.append(batch);

    overlay.hidden = false;
    document.body.classList.add('modal-open');

    current = Math.min(items.length - 1, Math.max(0, index || 0));
    scale = 1;
    offsetX = 0;
    offsetY = 0;
    dragX = 0;
    dragY = 0;

    measure();
    place(false);
    chrome();
    hint();

    // 当前这张的压缩图立刻顶上，页面上刚看过，多半是现成的
    ensurePreview(items[current]);
    window.clearTimeout(settleTimer);
    settleTimer = window.setTimeout(settle, 0);

    overlay.querySelector('[data-act="close"]').focus();
  };

  /* ---- 交互 ---- */

  const bind = (node) => {
    // 按钮只认 click：指针那套在 pointerdown 里就把它们放过去了
    node.addEventListener('click', (event) => {
      const button = event.target.closest('[data-act]');
      if (!button) return;

      const act = button.dataset.act;
      if (act === 'close') close();
      else if (act === 'prev') step(-1);
      else if (act === 'next') step(1);
      else if (act === 'in') zoomTo(scale * 1.5, null);
      else if (act === 'out') zoomTo(scale / 1.5, null);
    });

    node.addEventListener('wheel', (event) => {
      event.preventDefault();
      const factor = Math.exp(-event.deltaY * 0.0015);
      zoomTo(scale * factor, stageOrigin(event.clientX, event.clientY), false);
    }, { passive: false });

    document.addEventListener('keydown', (event) => {
      if (!overlay || overlay.hidden) return;

      switch (event.key) {
        case 'Escape': event.preventDefault(); close(); break;
        case 'ArrowLeft': event.preventDefault(); step(-1); break;
        case 'ArrowRight': event.preventDefault(); step(1); break;
        case '+': case '=': event.preventDefault(); zoomTo(scale * 1.5, null); break;
        case '-': event.preventDefault(); zoomTo(scale / 1.5, null); break;
        case '0': event.preventDefault(); resetZoom(); break;
        case 'Tab': trapFocus(event); break;
        default: break;
      }
    });

    // 转屏之后一格的宽度变了，位置得重新按新宽度算
    window.addEventListener('resize', () => {
      if (!overlay || overlay.hidden) return;
      measure();
      place(false);
    });

    bindPointer(node);
  };

  // 打开着的时候焦点不许跑到后面的页面上去
  const trapFocus = (event) => {
    const focusable = Array.from(overlay.querySelectorAll('button:not([hidden]):not([disabled])'));
    if (!focusable.length) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  /*
   * 使用 Pointer Events 统一处理鼠标与触摸：
   * - 单指：缩放时拖动画面，否则锁定方向后横向翻页或下滑关闭。
   * - 双指：捏合缩放。
   * - 点击：点击图片外关闭，双击切换缩放状态。
   *
   * 拖动更新通过 requestAnimationFrame 合帧，每帧仅提交一次 transform，
   * 避免高频 pointermove 阻塞主线程，保证滑动流畅。
   *
   * Pointer Capture 会改变 click 事件 target，
   * 因此在 pointerdown 阶段提前记录命中区域。
   */
  const bindPointer = (node) => {
    const active = new Map();
    let startX = 0;
    let startY = 0;
    let baseX = 0;
    let baseY = 0;
    let baseScale = 1;
    let baseSpan = 0;
    let axis = '';           // '' 还没定 / 'x' 翻页 / 'y' 下滑关闭
    let dragging = false;
    let onFrame = false;
    let lastTap = 0;
    let trail = [];          // 最近这一小段的轨迹，用来算甩出去的速度
    let painting = 0;
    let pending = false;

    // 一帧只写一次 transform：指针事件来得比屏幕刷得还密，写多了纯属浪费
    const paint = () => {
      pending = false;
      painting = 0;
      if (dragging) applyZoom(false);
      else place(false);
    };

    const schedule = () => {
      if (pending) return;
      pending = true;
      painting = window.requestAnimationFrame(paint);
    };

    const stopPaint = () => {
      if (painting) window.cancelAnimationFrame(painting);
      painting = 0;
      pending = false;
    };

    const span = () => {
      const points = Array.from(active.values());
      return Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y);
    };

    const centre = () => {
      const points = Array.from(active.values());
      return { x: (points[0].x + points[1].x) / 2, y: (points[0].y + points[1].y) / 2 };
    };

    /**
     * 甩出去的速度，px/ms。
     *
     * 只看最后两个点会被抖动带偏，所以取最近一小段的位移除以这段的时长；
     * 这段太短（不到 SPEED_SPAN）就当没测出来，交给距离去判断。
     */
    const velocity = () => {
      if (trail.length < 2) return 0;

      const head = trail[0];
      const tail = trail[trail.length - 1];
      const gap = tail.t - head.t;

      return gap >= SPEED_SPAN ? (tail.x - head.x) / gap : 0;
    };

    // 松手：拖得够远或者甩得够快就翻过去，否则弹回来
    const settleSwipe = () => {
      const reach = Math.min(SWIPE_CAP, stageWidth * SWIPE_PART);
      const speed = velocity();
      const far = Math.abs(dragX) > reach;
      const fast = Math.abs(speed) > SWIPE_SPEED && (speed < 0) === (dragX < 0);

      if (far || fast) step(dragX < 0 ? 1 : -1);
      else goTo(current, true);
    };

    const settleDrop = () => {
      if (dragY > CLOSE_DROP) close();
      else goTo(current, true);
    };

    // 没挪动过的那一下：点图之外关掉，连着点图两下切换放大
    const settleTap = (clientX, clientY) => {
      if (!onFrame) {
        close();
        return;
      }

      const now = Date.now();
      if (now - lastTap < 320) {
        lastTap = 0;
        zoomTo(scale > 1.001 ? MIN_SCALE : 2.5, stageOrigin(clientX, clientY));
      } else {
        lastTap = now;
      }
    };

    node.addEventListener('pointerdown', (event) => {
      // 按钮那几个交给 click，别把指针捕获走
      if (event.target.closest('[data-act]')) return;

      active.set(event.pointerId, { x: event.clientX, y: event.clientY });

      // 捕获拿不到就算了（这一指已经被别处收走），后面照样按坐标算
      try {
        if (node.setPointerCapture) node.setPointerCapture(event.pointerId);
      } catch (ignored) {
        // 没有捕获也能跑，不值得为它中断这一划
      }

      if (active.size === 2) {
        baseSpan = span();
        baseScale = scale;
        axis = '';
        dragging = false;
        node.classList.remove('is-swiping');
        return;
      }

      measure();
      startX = event.clientX;
      startY = event.clientY;
      trail = [{ x: event.clientX, t: event.timeStamp }];
      baseX = offsetX;
      baseY = offsetY;
      axis = '';
      onFrame = event.target.closest('.lightbox-frame') !== null;
      dragging = scale > 1.001;
      if (dragging) node.classList.add('is-dragging');
    });

    node.addEventListener('pointermove', (event) => {
      if (!active.has(event.pointerId)) return;
      active.set(event.pointerId, { x: event.clientX, y: event.clientY });

      if (active.size >= 2) {
        if (baseSpan > 0) zoomTo(baseScale * (span() / baseSpan), stageOrigin(centre().x, centre().y), false);
        return;
      }

      const dx = event.clientX - startX;
      const dy = event.clientY - startY;

      if (dragging) {
        offsetX = baseX + dx;
        offsetY = baseY + dy;
        schedule();
        return;
      }

      // 方向先锁死：一划要么翻页要么下滑，两件事掺一起就会抖
      if (!axis) {
        if (Math.abs(dx) < AXIS_LOCK && Math.abs(dy) < AXIS_LOCK) return;
        axis = Math.abs(dx) > Math.abs(dy) ? 'x' : 'y';
        node.classList.add('is-swiping');
      }

      if (axis === 'x') {
        // 到头了还接着拖就只跟一点点，手上能感觉到「没有下一张了」
        const edge = (dx > 0 && current === 0) || (dx < 0 && current === items.length - 1);
        dragX = edge ? dx * RUBBER : dx;
        dragY = 0;
      } else {
        dragY = dy > 0 ? dy : dy * RUBBER;
        dragX = 0;
      }

      // 只留最近这一小段，早的丢掉：松手时算的就是「最后甩的那一下」
      trail.push({ x: event.clientX, t: event.timeStamp });
      while (trail.length > 2 && event.timeStamp - trail[0].t > SPEED_WINDOW) trail.shift();

      schedule();
    });

    const release = (event) => {
      if (!active.has(event.pointerId)) return;

      const dx = event.clientX - startX;
      const dy = event.clientY - startY;
      const moved = Math.abs(dx) > AXIS_LOCK || Math.abs(dy) > AXIS_LOCK;
      active.delete(event.pointerId);

      node.classList.remove('is-dragging');

      if (active.size >= 1) {
        // 松开一根手指后，剩下那根重新算起点，别让图猛地跳一下
        const rest = Array.from(active.values())[0];
        startX = rest.x;
        startY = rest.y;
        baseX = offsetX;
        baseY = offsetY;
        dragging = scale > 1.001;
        axis = '';
        return;
      }

      stopPaint();
      node.classList.remove('is-swiping');

      if (!moved && event.type === 'pointerup') settleTap(event.clientX, event.clientY);
      else if (axis === 'x') settleSwipe();
      else if (axis === 'y') settleDrop();
      else applyZoom(false);

      dragging = false;
      axis = '';
      baseSpan = 0;
      trail = [];
    };

    node.addEventListener('pointerup', release);
    node.addEventListener('pointercancel', release);
  };

  /* ---- 页面里的触发元素 ---- */

  // 比例尽量取原图的：正文那边写在 img 的 width/height 上，取不着就等压缩图自己报
  const ratioOf = (trigger, image, preview) => {
    const width = Number(trigger.dataset.width);
    const height = Number(trigger.dataset.height);
    if (width > 0 && height > 0) return width + ' / ' + height;

    if (!image) return '';

    // 页面上这张就是查看器要用的那张压缩图时，它报的尺寸才作数；
    // 后台图片库列的是裁过的封面，比例和原图对不上，宁可不写
    if ((image.currentSrc || image.src) !== preview) return '';

    const marked = Number(image.getAttribute('width'));
    const markedHeight = Number(image.getAttribute('height'));
    if (marked > 0 && markedHeight > 0) return marked + ' / ' + markedHeight;

    return image.naturalWidth > 0 ? image.naturalWidth + ' / ' + image.naturalHeight : '';
  };

  const read = (trigger) => {
    const image = trigger.querySelector('img');
    const preview = trigger.dataset.preview || (image ? image.currentSrc || image.src : '');

    return {
      full: trigger.dataset.full || preview,
      preview: preview,
      alt: trigger.dataset.alt || (image ? image.alt : ''),
      caption: trigger.dataset.caption || '',
      ratio: ratioOf(trigger, image, preview)
    };
  };

  /**
   * 认领 root 里所有 data-lightbox 触发元素。
   * 动态插进来的内容再调一次就行，已经认领过的不会重复绑。
   */
  const scan = (root) => {
    const scope = root || document;
    scope.querySelectorAll('[data-lightbox]:not([data-lightbox-ready])').forEach((trigger) => {
      trigger.dataset.lightboxReady = '1';
      trigger.addEventListener('click', (event) => {
        event.preventDefault();

        const group = trigger.dataset.lightbox;
        const peers = Array.from(document.querySelectorAll('[data-lightbox="' + group.replace(/"/g, '\\"') + '"]'));
        const index = Math.max(0, peers.indexOf(trigger));

        open(peers.map(read), index);
      });
    });
  };

  window.ccLightbox = { open: open, close: close, scan: scan };

  scan(document);
}());
