/* 把 Service Worker 挂上。通知要靠它来收，「添加到主屏幕」也要靠它。

   每个页面都会跑一遍，但注册是幂等的：装过了就什么都不做。
   注册失败不该影响任何东西 —— http 打开的站点、老浏览器都会走到这一支，
   站点该怎么用还怎么用，只是收不到通知 */
(function () {
  'use strict';

  if (!('serviceWorker' in navigator)) return;

  /* 作用域是整个站点，所以文件必须待在根目录 */
  window.addEventListener('load', function () {
    navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function () {});
  });
}());
