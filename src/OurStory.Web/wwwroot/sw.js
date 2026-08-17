/* Service Worker：通知的收件端。
   它跟页面是两回事 —— 页面关掉、浏览器只剩后台，它照样会被叫醒来收这一条。
   iPhone 上要先「添加到主屏幕」，这个文件才有机会跑起来。

   注意：这里放在站点根目录，作用域才是整个 /，
   移到 /assets 下面的话只能管到 /assets 那一片。 */
(function () {
  'use strict';

  self.addEventListener('install', () => self.skipWaiting());
  self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));
  self.addEventListener('fetch', () => {});

  const FALLBACK = {
    title: 'CC Our Story · 我们的故事',
    body: '你们的故事有了新的暖心动态',
    url: '/',
    tag: 'ourstory'
  };

  const read = (event) => {
    if (!event.data) return FALLBACK;
    try {
      return Object.assign({}, FALLBACK, event.data.json());
    } catch (e) {
      return Object.assign({}, FALLBACK, { body: event.data.text() });
    }
  };

  self.addEventListener('push', (event) => {
    const data = read(event);
    event.waitUntil(self.registration.showNotification(data.title, {
      body: data.body,
      tag: data.tag,
      icon: '/assets/icons/icon-192.png',
      badge: '/assets/icons/icon-192.png',
      renotify: true,
      data: { url: data.url }
    }));
  });

  self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const target = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin);

    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      for (const client of windows) {
        if (new URL(client.url).origin === target.origin && 'focus' in client) {
          if ('navigate' in client) client.navigate(target.href);
          return client.focus();
        }
      }

      return self.clients.openWindow ? self.clients.openWindow(target.href) : undefined;
    }));
  });

  self.addEventListener('pushsubscriptionchange', (event) => {
    event.waitUntil((async () => {
      const key = event.oldSubscription && event.oldSubscription.options
        ? event.oldSubscription.options.applicationServerKey
        : null;

      const fresh = event.newSubscription || (key
        ? await self.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key })
        : null);

      if (!fresh) return;

      const json = fresh.toJSON();
      await fetch('/api/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          endpoint: fresh.endpoint,
          p256dh: json.keys.p256dh,
          auth: json.keys.auth,
          previousEndpoint: event.oldSubscription ? event.oldSubscription.endpoint : null
        })
      });
    })());
  });
}());
