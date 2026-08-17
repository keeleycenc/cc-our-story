/* 后台「通知」页上的那几个按钮。

   浏览器有条硬规矩：请求通知权限必须由用户的点击直接触发，
   所以下面所有 requestPermission 都紧挨着 click 事件，
   中间不能先 await 一个网络请求再去要权限 —— 那样会被当成自动弹窗拒掉 */
(function () {
  'use strict';

  const panel = document.querySelector('[data-push]');
  if (!panel) return;

  const status = panel.querySelector('[data-push-status]');
  const enableButton = panel.querySelector('[data-push-enable]');
  const disableButton = panel.querySelector('[data-push-disable]');
  const testButton = panel.querySelector('[data-push-test]');
  const sendForm = document.querySelector('[data-push-send]');

  const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  const standalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
  const isApple = /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

  const say = (text, kind) => {
    if (!status) return;
    status.textContent = text;
    status.dataset.kind = kind || '';
  };

  const busy = (button, on) => {
    if (!button) return;
    button.disabled = on;
    button.classList.toggle('is-busy', on);
  };

  /* VAPID 公钥在页面上是 base64url 的字符串，subscribe 要的是字节数组 */
  const toBytes = (value) => {
    const padded = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    const raw = window.atob(padded);
    const bytes = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
    return bytes;
  };

  const post = async (url, body) => {
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify(body || {})
    });

    return response.json().catch(() => ({ ok: false, message: '暂时未收到服务器响应，请稍后再试。' }));
  };

  if (!supported) {
    say(isApple && !standalone
      ? '如需在 iPhone 或 iPad 上接收彼此的消息，请先在 Safari 中选择「分享 → 添加到主屏幕」，再从主屏幕图标打开本站并开启通知。'
      : '当前浏览器暂不支持网页通知，请使用 Chrome、Edge、Firefox 或 Safari 访问。', 'warn');
    busy(enableButton, true);
    busy(testButton, true);
    return;
  }

  if (window.isSecureContext === false) {
    say('为保障你们的信息安全，通知功能仅支持使用 HTTPS 的站点，请先完成安全证书配置。', 'warn');
    busy(enableButton, true);
    return;
  }

  const ready = () => navigator.serviceWorker.register('/sw.js', { scope: '/' })
    .then(() => navigator.serviceWorker.ready);

  const refresh = async () => {
    if (Notification.permission === 'denied') {
      say('当前设备的通知权限已被关闭。如需重新开启，请在浏览器的站点设置中将「通知」调整为允许。', 'warn');
      busy(enableButton, true);
      return;
    }

    const registration = await ready();
    const subscription = await registration.pushManager.getSubscription();

    if (subscription) {
      say('当前设备已开启通知，彼此的重要动态会及时送达。', 'ok');
    } else {
      say('当前设备尚未开启通知，开启后即可接收彼此的暖心动态。', '');
    }

    if (enableButton) enableButton.hidden = Boolean(subscription);
    if (disableButton) disableButton.hidden = !subscription;
    if (testButton) testButton.disabled = false;
  };

  if (enableButton) {
    enableButton.addEventListener('click', async () => {
      busy(enableButton, true);

      try {
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
          say(permission === 'denied'
            ? '当前设备未授予通知权限。如需开启，请前往浏览器的站点设置进行调整。'
            : '尚未获得通知授权，请再次点击并确认允许。', 'warn');
          return;
        }

        const keyResponse = await fetch('/api/push/key', { credentials: 'same-origin' });
        const key = await keyResponse.json();
        if (!key.ok || !key.key) {
          say('通知服务尚未完成配置，请联系站点管理员检查启动日志。', 'warn');
          return;
        }

        const registration = await ready();
        const subscription = await registration.pushManager.subscribe({
          /* 收到推送必须弹一条给用户看，浏览器不允许悄悄用它做别的事 */
          userVisibleOnly: true,
          applicationServerKey: toBytes(key.key)
        });

        const payload = subscription.toJSON();
        const result = await post('/api/push/subscribe', {
          endpoint: subscription.endpoint,
          p256dh: payload.keys.p256dh,
          auth: payload.keys.auth
        });

        if (!result.ok) {
          await subscription.unsubscribe().catch(() => {});
          say(result.message || '当前设备登记失败，请稍后重试。', 'warn');
          return;
        }

        say('通知已开启，今后不会错过彼此的重要动态。', 'ok');
        window.location.reload();
      } catch (error) {
        say('开启通知时遇到问题，请稍后重试：' + (error && error.message ? error.message : error), 'warn');
      } finally {
        busy(enableButton, false);
      }
    });
  }

  if (disableButton) {
    disableButton.addEventListener('click', async () => {
      busy(disableButton, true);

      try {
        const registration = await ready();
        const subscription = await registration.pushManager.getSubscription();
        if (!subscription) {
          window.location.reload();
          return;
        }

        /* 两头都要撤：浏览器这边退订，后台那边把设备记录删掉 */
        await post('/api/push/unsubscribe', { endpoint: subscription.endpoint });
        await subscription.unsubscribe().catch(() => {});
        window.location.reload();
      } finally {
        busy(disableButton, false);
      }
    });
  }

  if (testButton) {
    testButton.addEventListener('click', async () => {
      busy(testButton, true);
      say('正在发送测试通知……', '');

      try {
        const result = await post('/api/push/test');
        say(result.message || (result.ok ? '测试通知已发送。' : '测试通知暂未发送成功，请稍后再试。'), result.ok ? 'ok' : 'warn');
      } finally {
        busy(testButton, false);
      }
    });
  }

  if (sendForm) {
    const input = sendForm.querySelector('[data-push-body]');
    const feedback = sendForm.querySelector('[data-push-feedback]');
    const button = sendForm.querySelector('button');

    sendForm.addEventListener('submit', async (event) => {
      event.preventDefault();

      const body = (input.value || '').trim();
      if (!body) {
        input.focus();
        return;
      }

      busy(button, true);

      try {
        const result = await post('/api/push/send', { body: body });
        if (feedback) {
          feedback.textContent = result.message || (result.ok ? '心意已送达。' : '心意暂未送达，请稍后再试。');
          feedback.dataset.kind = result.ok ? 'ok' : 'warn';
        }

        if (result.ok) input.value = '';
      } finally {
        busy(button, false);
      }
    });
  }

  refresh().catch(() => say('暂时无法读取当前设备的通知状态，请刷新页面后重试。', 'warn'));
}());
