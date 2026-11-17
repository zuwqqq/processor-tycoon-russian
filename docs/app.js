(() => {
  'use strict';

  const configured = String(window.RELEASE_REPOSITORY || '').trim();
  const pageHost = window.location.hostname.match(/^([a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\.github\.io$/i);
  const pathRepository = window.location.pathname.split('/').filter(Boolean)[0] || '';
  const detected = pageHost && pathRepository && !['index.html', '.', '..'].includes(pathRepository)
    ? `${pageHost[1]}/${pathRepository}`
    : '';
  const repository = configured || detected;
  const validRepository = /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\/[a-z0-9_.-]+$/i.test(repository)
    && !['.', '..'].includes(repository.split('/')[1]);

  if (!validRepository) return;

  const repositoryUrl = `https://github.com/${repository}`;
  document.querySelectorAll('[data-download]').forEach(link => {
    link.href = `${repositoryUrl}/releases/latest/download/ProcessorTycoon-Russian-Setup.exe`;
    link.removeAttribute('aria-disabled');
    link.removeAttribute('tabindex');
  });
  document.querySelectorAll('[data-repository]').forEach(link => {
    link.href = repositoryUrl;
    link.hidden = false;
  });
  const status = document.getElementById('download-status');
  if (status) status.textContent = '';
})();
