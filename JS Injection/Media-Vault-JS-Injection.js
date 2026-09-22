/* User Script: Media Vault */
(function() { try {
/* User Script: Media Vault */
(function() { try {
/* User Script: Media Vault */
(function() { try {
/* User Script: Media Vault */
(function() {
    'use strict';

    try {
        if (window.__MEDIA_VAULT_WEB_UI_LOADED__) {
            return;
        }
        window.__MEDIA_VAULT_WEB_UI_LOADED__ = true;

        console.log("[INJECTOR] public.js top-level execution start");

        var CONFIG = {
            buildId: '2026-09-07-es5-tizen-scoped-v8-fixed',
            pollIntervalMs: 750,
            mutationDebounceMs: 50,
            startupDelayMs: 250,
            requestTimeoutMs: 10000,
            detailPageWaitMs: 15000,
            styleId: 'media-vault-web-ui-styles',
            rootClass: 'media-vault-web-ui',
            panelId: 'media-vault-download-panel',
            actionButtonId: 'media-vault-action-button'
        };

        var isTizenDevice = function() {
            var ua = (navigator && navigator.userAgent) ? navigator.userAgent.toLowerCase() : '';
            return ua.indexOf('tizen') !== -1 || ua.indexOf('samsungtv') !== -1;
        };

        var activeItemId = null;
        var activeMediaVaultId = null;
        var activeItem = null;
        var lastStatus = null;
        var nonMediaVaultCache = {};

        var pollTimer = null;
        var navigationTimer = null;
        var mutationTimer = null;

        var renderGeneration = 0;
        var mutationObserver = null;
        var actionBusy = false;

        var CLOUD_SVG_HTML = '<svg class="mv-cloud-svg" viewBox="0 0 24 24" width="24" height="24" fill="currentColor" aria-hidden="true"><path d="M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96z"/></svg>';

        var removeTestBanner = function() {
            var bannerElements = document.querySelectorAll('*');
            for (var i = 0; i < bannerElements.length; i++) {
                var el = bannerElements[i];
                if (el && el.textContent && el.textContent.indexOf('APPS2SAMSUNG WGT TEST') !== -1) {
                    if (el.parentNode && el.tagName !== 'BODY' && el.tagName !== 'HTML') {
                        el.parentNode.removeChild(el);
                    }
                }
            }
        };

        var log = function() {
            if (console && console.debug) {
                console.debug.apply(console, ['[Media Vault]'].concat(Array.prototype.slice.call(arguments)));
            }
        };

        var warn = function() {
            if (console && console.warn) {
                console.warn.apply(console, ['[Media Vault]'].concat(Array.prototype.slice.call(arguments)));
            }
        };

        var getApiClient = function() {
            return window.ApiClient || null;
        };

        var getServerAddress = function() {
            var api = getApiClient();
            try {
                if (api && typeof api.serverAddress === 'function') {
                    var address = api.serverAddress();
                    if (typeof address === 'string' && address.trim()) {
                        return address.replace(/\/+$/, '');
                    }
                }
            } catch (error) {
                warn('Could not read Jellyfin serverAddress()', error);
            }

            var fallbackAddress = api ? api._serverAddress : null;
            if (typeof fallbackAddress === 'string' && fallbackAddress.trim()) {
                return fallbackAddress.replace(/\/+$/, '');
            }

            return window.location.origin;
        };

        var getAccessToken = function() {
            var api = getApiClient();
            try {
                if (api && typeof api.accessToken === 'function') {
                    return api.accessToken() || '';
                }
            } catch (error) {
                warn('Could not read Jellyfin access token', error);
            }
            return '';
        };

        var getCurrentUserId = function() {
            var api = getApiClient();
            try {
                if (api && api._serverInfo && api._serverInfo.UserId) {
                    return api._serverInfo.UserId;
                }
                if (api && typeof api.getCurrentUserId === 'function') {
                    return api.getCurrentUserId() || '';
                }
            } catch (error) {
                warn('Could not read Jellyfin user ID', error);
            }
            return '';
        };

        var buildUrl = function(path) {
            return getServerAddress() + '/' + String(path).replace(/^\/+/, '');
        };

        var fetchJson = function(path, options, callback) {
            options = options || {};
            var url = buildUrl(path);
            var xhr = new XMLHttpRequest();
            xhr.open(options.method || 'GET', url, true);
            xhr.setRequestHeader('Accept', 'application/json');

            var token = getAccessToken();
            if (token) {
                xhr.setRequestHeader('X-Emby-Token', token);
                xhr.setRequestHeader('Authorization', 'MediaBrowser Token="' + token + '"');
            }

            if (options.headers) {
                for (var k in options.headers) {
                    if (options.headers.hasOwnProperty(k)) {
                        xhr.setRequestHeader(k, options.headers[k]);
                    }
                }
            }

            xhr.timeout = CONFIG.requestTimeoutMs;

            xhr.onload = function() {
                if (xhr.status >= 200 && xhr.status < 300) {
                    if (xhr.status === 204 || !xhr.responseText) {
                        callback(null, null);
                    } else {
                        try {
                            var parsed = JSON.parse(xhr.responseText);
                            callback(null, parsed);
                        } catch (e) {
                            callback(e, null);
                        }
                    }
                } else {
                    callback(new Error(xhr.status + ' ' + xhr.statusText + ': ' + xhr.responseText), null);
                }
            };

            xhr.onerror = function() {
                callback(new Error('Network error connecting to Media Vault endpoint'), null);
            };

            xhr.ontimeout = function() {
                callback(new Error('Request timed out'), null);
            };

            xhr.send(options.body || null);
        };

        var getHashParams = function() {
            var sources = [
                window.location.hash || '',
                window.location.search || ''
            ];

            for (var i = 0; i < sources.length; i++) {
                var source = sources[i];
                var queryIndex = source.indexOf('?');
                if (queryIndex !== -1) {
                    var params = new URLSearchParams(source.slice(queryIndex + 1));
                    if (params.get('id')) {
                        return params;
                    }
                }
                if (source.indexOf('?') === 0) {
                    var queryParams = new URLSearchParams(source.slice(1));
                    if (queryParams.get('id')) {
                        return queryParams;
                    }
                }
            }
            return new URLSearchParams();
        };

        var getVisibleDetailPage = function() {
            var pages = Array.prototype.slice.call(
                document.querySelectorAll('#itemDetailPage, .itemDetailPage, [data-role="page"].itemDetailPage')
            );

            for (var i = 0; i < pages.length; i++) {
                var page = pages[i];
                var style = window.getComputedStyle(page);
                var rect = page.getBoundingClientRect();

                if (!page.classList.contains('hide') && style.display !== 'none' && style.visibility !== 'hidden' && rect.width >= 0) {
                    return page;
                }
            }
            return null;
        };

        var getCurrentItemId = function() {
            var params = getHashParams();
            var id = params.get('id');
            if (id) {
                return id;
            }

            var route = (window.location.hash || '') + ' ' + (window.location.pathname || '');
            var routeMatch = route.match(/(?:^|[\/#])details(?:\/|%2F)([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i);
            if (routeMatch && routeMatch[1]) {
                return routeMatch[1];
            }

            var detailPage = getVisibleDetailPage();
            if (!detailPage) {
                return null;
            }

            return (
                (detailPage.dataset && detailPage.dataset.id) ||
                detailPage.getAttribute('data-id') ||
                detailPage.getAttribute('data-item-id') ||
                (detailPage.querySelector('[data-id]') && detailPage.querySelector('[data-id]').getAttribute('data-id')) ||
                (detailPage.querySelector('[data-item-id]') && detailPage.querySelector('[data-item-id]').getAttribute('data-item-id')) ||
                null
            );
        };

        var getMainDetailButtons = function(detailPage) {
            if (!detailPage) {
                return null;
            }
            return detailPage.querySelector('.mainDetailButtons') || null;
        };

        var getRuntimeMinutes = function(item) {
            var ticks = Number(item && item.RunTimeTicks);
            if (isFinite(ticks) && ticks > 0) {
                return ticks / 600000000;
            }
            return null;
        };

        var getJellyfinItem = function(itemId, callback) {
            var api = getApiClient();
            var userId = getCurrentUserId();

            if (api && typeof api.getItem === 'function' && userId) {
                try {
                    var promise = api.getItem(userId, itemId);
                    if (promise && typeof promise.then === 'function') {
                        promise.then(function(res) { callback(null, res); }).catch(function(err) { callback(err, null); });
                        return;
                    }
                } catch (e) {
                    warn('api.getItem call failed, falling back to fetchJson', e);
                }
            }

            var query = 'fields=ProviderIds,Tags,RunTimeTicks,MediaSources,Type,Name,ProductionYear,OfficialRating,CommunityRating';
            if (userId) {
                query += '&userId=' + userId;
            }

            fetchJson('/Items/' + encodeURIComponent(itemId) + '?' + query, {}, callback);
        };

        var getMediaVaultId = function(item) {
            var ids = item ? item.ProviderIds : null;
            if (ids) {
                var providerId = ids.MediaVault || ids.mediavault || ids['Media Vault'] || ids['media-vault'];
                if (providerId) {
                    return String(providerId);
                }
            }
            return null;
        };

        var findMediaVaultIdFromCatalogue = function(item, callback) {
            if (!item) {
                callback(null, null);
                return;
            }

            var tags = Array.isArray(item.Tags) ? item.Tags.map(function(v) { return String(v).toLowerCase(); }) : [];
            if (tags.indexOf('media vault') === -1) {
                callback(null, null);
                return;
            }

            fetchJson('/MediaVault/Downloads/catalogue', {}, function(err, catalogue) {
                if (err || !Array.isArray(catalogue)) {
                    callback(null, null);
                    return;
                }

                var title = String(item.Name || '').trim().toLowerCase();
                var type = String(item.Type || '').trim().toLowerCase();

                for (var i = 0; i < catalogue.length; i++) {
                    var entry = catalogue[i];
                    var entryTitle = String(entry.Title || '').trim().toLowerCase();
                    var entryType = String(entry.Type || '').trim().toLowerCase();

                    if (entryTitle === title) {
                        if (type === 'movie' && entryType === 'movie') {
                            callback(null, String(entry.MediaVaultId));
                            return;
                        }
                        if (type === 'episode' && entryType === 'episode') {
                            callback(null, String(entry.MediaVaultId));
                            return;
                        }
                        if (type !== 'movie' && type !== 'episode') {
                            callback(null, String(entry.MediaVaultId));
                            return;
                        }
                    }
                }
                callback(null, null);
            });
        };

        var getRawNumber = function(object) {
            var names = Array.prototype.slice.call(arguments, 1);
            for (var i = 0; i < names.length; i++) {
                var name = names[i];
                if (object && object[name] !== undefined && object[name] !== null) {
                    var val = Number(object[name]);
                    if (isFinite(val)) {
                        return val;
                    }
                }
            }
            return 0;
        };

        var getRawString = function(object) {
            var names = Array.prototype.slice.call(arguments, 1);
            for (var i = 0; i < names.length; i++) {
                var name = names[i];
                if (object && object[name] !== undefined && object[name] !== null) {
                    var val = String(object[name]);
                    if (val) {
                        return val;
                    }
                }
            }
            return '';
        };

        var normaliseStatus = function(raw, runtimeMinutes) {
            if (!raw) {
                return {
                    mediaVaultId: activeMediaVaultId,
                    filename: (activeItem && activeItem.Name) || 'Media Vault item',
                    status: 'not-downloaded',
                    downloadedBytes: 0,
                    totalBytes: 0,
                    progress: 0,
                    speedBytesPerSecond: 0,
                    etaSeconds: null,
                    runtimeMinutes: runtimeMinutes,
                    cachedRuntimeMinutes: null,
                    cachedRuntimePercentage: null,
                    error: null,
                    receivedAt: Date.now()
                };
            }

            var downloadedBytes = getRawNumber(raw, 'DownloadedBytes', 'downloadedBytes', 'bytes_downloaded');
            var totalBytes = getRawNumber(raw, 'TotalBytes', 'totalBytes', 'total_bytes');
            var progress = getRawNumber(raw, 'Progress', 'progress');

            if (totalBytes > 0) {
                progress = (downloadedBytes / totalBytes) * 100;
            } else if (progress >= 0 && progress <= 1) {
                progress *= 100;
            }
            progress = Math.max(0, Math.min(100, progress));

            var speedBytesPerSecond = getRawNumber(raw, 'SpeedBytesPerSecond', 'speedBytesPerSecond', 'speed_bytes_per_second');
            var etaRaw = raw.EtaSeconds !== undefined ? raw.EtaSeconds : (raw.etaSeconds !== undefined ? raw.etaSeconds : raw.eta_seconds);
            var etaNumber = Number(etaRaw);

            var apiRuntime = getRawNumber(raw, 'RuntimeMinutes', 'runtimeMinutes');
            var cachedRuntimeRaw = raw.CachedRuntimeMinutes !== undefined ? raw.CachedRuntimeMinutes : raw.cachedRuntimeMinutes;
            var cachedRuntimeNumber = Number(cachedRuntimeRaw);

            var cachedPercentageRaw = raw.CachedRuntimePercentage !== undefined ? raw.CachedRuntimePercentage : raw.cachedRuntimePercentage;
            var cachedPercentageNumber = Number(cachedPercentageRaw);

            return {
                raw: raw,
                mediaVaultId: getRawString(raw, 'MediaVaultId', 'mediaVaultId') || activeMediaVaultId,
                filename: getRawString(raw, 'Filename', 'filename', 'Title', 'title') || (activeItem && activeItem.Name) || 'Media Vault item',
                status: getRawString(raw, 'Status', 'status') || 'not-downloaded',
                downloadedBytes: downloadedBytes,
                totalBytes: totalBytes,
                progress: progress,
                speedBytesPerSecond: speedBytesPerSecond,
                etaSeconds: isFinite(etaNumber) && etaNumber >= 0 ? etaNumber : null,
                runtimeMinutes: apiRuntime > 0 ? apiRuntime : runtimeMinutes,
                cachedRuntimeMinutes: isFinite(cachedRuntimeNumber) && cachedRuntimeNumber >= 0 ? cachedRuntimeNumber : null,
                cachedRuntimePercentage: isFinite(cachedPercentageNumber) && cachedPercentageNumber >= 0 ? cachedPercentageNumber : null,
                error: getRawString(raw, 'Error', 'error') || null,
                receivedAt: Date.now()
            };
        };

        var getClientCalculatedSpeed = function(status) {
            if (isFinite(status.speedBytesPerSecond) && status.speedBytesPerSecond > 0) {
                return status.speedBytesPerSecond;
            }

            if (!lastStatus) {
                return 0;
            }

            var previousBytes = Number(lastStatus.downloadedBytes || 0);
            var previousTime = Number(lastStatus.receivedAt || 0);

            var currentBytes = Number(status.downloadedBytes || 0);
            var currentTime = Number(status.receivedAt || 0);

            var byteDelta = currentBytes - previousBytes;
            var timeDelta = (currentTime - previousTime) / 1000;

            if (byteDelta <= 0 || timeDelta <= 0 || timeDelta < 0.25) {
                return 0;
            }

            return byteDelta / timeDelta;
        };

        var getRuntimeCacheMinutes = function(status) {
            var runtimeMinutes = Number(status.runtimeMinutes);
            if (!isFinite(runtimeMinutes) || runtimeMinutes <= 0) {
                return null;
            }

            if (isFinite(status.cachedRuntimeMinutes) && status.cachedRuntimeMinutes >= 0) {
                return Math.min(runtimeMinutes, status.cachedRuntimeMinutes);
            }

            if (status.totalBytes > 0) {
                return runtimeMinutes * Math.max(0, Math.min(1, status.downloadedBytes / status.totalBytes));
            }

            return null;
        };

        var formatBytes = function(bytes) {
            var value = Number(bytes);

            if (!isFinite(value) || value <= 0) {
                return '0 B';
            }

            var units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
            var index = 0;
            var scaled = value;

            while (scaled >= 1024 && index < units.length - 1) {
                scaled /= 1024;
                index += 1;
            }

            if (index === 0) {
                return Math.round(scaled) + ' ' + units[index];
            }

            if (scaled >= 100) {
                return scaled.toFixed(0) + ' ' + units[index];
            }

            if (scaled >= 10) {
                return scaled.toFixed(1) + ' ' + units[index];
            }

            return scaled.toFixed(2) + ' ' + units[index];
        };

        var formatSpeed = function(bytesPerSecond) {
            var speed = Number(bytesPerSecond);

            if (!isFinite(speed) || speed <= 0) {
                return '—';
            }

            return formatBytes(speed) + '/s';
        };

        var formatDuration = function(seconds) {
            var value = Number(seconds);

            if (!isFinite(value) || value < 0) {
                return '—';
            }

            var rounded = Math.round(value);
            var hours = Math.floor(rounded / 3600);
            var minutes = Math.floor((rounded % 3600) / 60);
            var remainingSeconds = rounded % 60;

            if (hours > 0) {
                return hours + 'h ' + minutes + 'm';
            }

            if (minutes > 0) {
                return minutes + 'm ' + remainingSeconds + 's';
            }

            return remainingSeconds + 's';
        };

        var formatRuntime = function(minutes) {
            var value = Number(minutes);

            if (!isFinite(value) || value <= 0) {
                return '—';
            }

            return formatDuration(value * 60);
        };

        var escapeHtml = function(value) {
            var element = document.createElement('div');
            element.textContent = value !== undefined && value !== null ? value : '';
            return element.innerHTML;
        };

        var createStyles = function() {
            if (document.getElementById(CONFIG.styleId)) {
                return;
            }

            var style = document.createElement('style');
            style.id = CONFIG.styleId;

            style.textContent = '' +
                '.' + CONFIG.rootClass + ' {' +
                    'width: 100%;' +
                    'box-sizing: border-box;' +
                    'margin: 1.15rem 0 1.35rem;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-panel {' +
                    'width: 100%;' +
                    'box-sizing: border-box;' +
                    'padding: 20px 22px;' +
                    'border: 1px solid rgba(255, 255, 255, .10);' +
                    'border-radius: 16px;' +
                    'background: linear-gradient(135deg, rgba(28, 39, 63, .92), rgba(18, 27, 46, .97));' +
                    'box-shadow: 0 12px 35px rgba(0, 0, 0, .20);' +
                    'color: inherit;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-header {' +
                    'display: flex;' +
                    'align-items: center;' +
                    'gap: 10px;' +
                    'margin-bottom: 16px;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-cloud {' +
                    'display: inline-flex;' +
                    'align-items: center;' +
                    'justify-content: center;' +
                    'flex: 0 0 32px;' +
                    'width: 32px;' +
                    'height: 32px;' +
                    'margin-right: 8px;' +
                    'border-radius: 99px;' +
                    'background: rgba(70, 150, 255, .14);' +
                    'color: #7db7ff;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-cloud-svg {' +
                    'display: block;' +
                    'width: 20px;' +
                    'height: 20px;' +
                    'fill: currentColor;' +
                '}' +
                'body.mv-is-tizen .' + CONFIG.rootClass + ' .mv-cloud,' +
                '@media tv, (tv), (device-aspect-ratio: 16/9) and (hover: none) {' +
                    '.' + CONFIG.rootClass + ' .mv-cloud {' +
                        'flex: 0 0 44px !important;' +
                        'width: 44px !important;' +
                        'height: 44px !important;' +
                        'margin-right: 12px !important;' +
                    '}' +
                    '.' + CONFIG.rootClass + ' .mv-cloud-svg {' +
                        'width: 28px !important;' +
                        'height: 28px !important;' +
                    '}' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-title {' +
                    'font-size: .92rem;' +
                    'font-weight: 800;' +
                    'letter-spacing: .08em;' +
                    'text-transform: uppercase;' +
                    'opacity: .96;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-subtitle {' +
                    'margin-top: 2px;' +
                    'color: rgba(255, 255, 255, .60);' +
                    'font-size: .78rem;' +
                    'overflow-wrap: anywhere;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-status-row {' +
                    'display: flex;' +
                    'align-items: baseline;' +
                    'justify-content: space-between;' +
                    'gap: 16px;' +
                    'margin-bottom: 10px;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-status {' +
                    'font-size: 1rem;' +
                    'font-weight: 700;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-status.mv-active,' +
                '.' + CONFIG.rootClass + ' .mv-status.mv-complete {' +
                    'color: #6ee7a1;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-status.mv-cancelled,' +
                '.' + CONFIG.rootClass + ' .mv-status.mv-failed {' +
                    'color: #ff7f96;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-status.mv-starting {' +
                    'color: #ffd166;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-progress {' +
                    'position: relative;' +
                    'width: 100%;' +
                    'height: 14px;' +
                    'overflow: hidden;' +
                    'border-radius: 999px;' +
                    'background: rgba(92, 110, 148, .35);' +
                    'box-shadow: inset 0 1px 2px rgba(0, 0, 0, .28);' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-progress-fill {' +
                    'width: 0%;' +
                    'height: 100%;' +
                    'border-radius: inherit;' +
                    'background: linear-gradient(90deg, #4ade80, #22c55e);' +
                    'box-shadow: 0 0 16px rgba(74, 222, 128, .16);' +
                    'transition: width .45s ease;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-progress-label {' +
                    'color: rgba(255, 255, 255, .74);' +
                    'font-size: .86rem;' +
                    'font-weight: 700;' +
                    'white-space: nowrap;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-stats {' +
                    'display: grid;' +
                    'grid-template-columns: minmax(0, 1fr) auto auto;' +
                    'gap: 10px 26px;' +
                    'align-items: center;' +
                    'margin-top: 12px;' +
                    'font-size: .9rem;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-stat {' +
                    'min-width: 0;' +
                    'color: rgba(255, 255, 255, .78);' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-stat strong {' +
                    'color: inherit;' +
                    'font-weight: 700;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-runtime-row {' +
                    'display: flex;' +
                    'align-items: center;' +
                    'justify-content: space-between;' +
                    'gap: 18px;' +
                    'margin-top: 12px;' +
                    'color: rgba(255, 255, 255, .64);' +
                    'font-size: .86rem;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-runtime-row strong {' +
                    'color: rgba(255, 255, 255, .88);' +
                '}' +
                /* Media Vault Action Button: icon-only, transparent until focused */
                '.mainDetailButtons #' + CONFIG.actionButtonId + ' {' +
                    'position: relative;' +
                    'display: inline-flex;' +
                    'align-items: center;' +
                    'justify-content: center;' +
                    'flex: 0 0 48px;' +
                    'width: 48px;' +
                    'height: 48px;' +
                    'min-width: 48px;' +
                    'min-height: 48px;' +
                    'padding: 0;' +
                    'margin: 0;' +
                    'box-sizing: border-box;' +
                    'overflow: hidden;' +
                    'pointer-events: auto;' +
                    'touch-action: manipulation;' +
                    'user-select: none;' +
                    'visibility: visible;' +
                    'opacity: 1;' +
                    'border: 0;' +
                    'border-radius: 12px;' +
                    'background: transparent;' +
                    'font-family: inherit;' +
                    'font-size: 0;' +
                    'font-weight: 700;' +
                    'cursor: pointer;' +
                    'transition: background .18s ease, transform .15s ease, box-shadow .18s ease;' +
                '}' +
                /* Unfocused states: no filled button background, only the action icon */
                '.mainDetailButtons #' + CONFIG.actionButtonId + '.mv-primary {' +
                    'background: transparent;' +
                    'color: #6ee7a1;' +
                '}' +
                '.mainDetailButtons #' + CONFIG.actionButtonId + '.mv-danger {' +
                    'background: transparent;' +
                    'color: #ff7f96;' +
                '}' +
                '.mainDetailButtons #' + CONFIG.actionButtonId + ':not(.mv-primary):not(.mv-danger) {' +
                    'background: transparent;' +
                    'color: rgba(255, 255, 255, .82);' +
                '}' +
                /* Keep mouse hover subtle; Tizen selection is handled separately below */
                '.mainDetailButtons #' + CONFIG.actionButtonId + ':hover:not(:disabled) {' +
                    'background: transparent;' +
                    'transform: none;' +
                    'box-shadow: none;' +
                '}' +
                /* Keyboard focus outside Tizen: no blue fill, preserving the normal web UI */
                '.mainDetailButtons #' + CONFIG.actionButtonId + ':focus {' +
                    'outline: none !important;' +
                    'background: transparent;' +
                    'transform: none;' +
                    'box-shadow: none;' +
                    'z-index: 1000;' +
                '}' +
                '.mainDetailButtons #' + CONFIG.actionButtonId + ':focus-visible {' +
                    'outline: none !important;' +
                    'background: transparent;' +
                    'transform: none;' +
                    'box-shadow: none;' +
                    'z-index: 1000;' +
                '}' +
                /* Button Icons */
                '.mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-icon {' +
                    'pointer-events: none;' +
                    'display: inline-flex;' +
                    'align-items: center;' +
                    'justify-content: center;' +
                    'width: 32px;' +
                    'height: 32px;' +
                    'margin: 0;' +
                    'flex: 0 0 32px;' +
                '}' +
                '.mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-icon svg {' +
                    'pointer-events: none;' +
                    'display: block;' +
                    'width: 60px;' +
                    'height: 60px;' +
                '}' +
                '.mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-label {' +
                    'display: none !important;' +
                '}' +
                /* Tizen TV Focus: fill the whole button area with Jellyfin\'s blue selected state */
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ' {' +
                    'width: 104.22px !important;' +
                    'height: 104.22px !important;' +
                    'min-width: 104.22px !important;' +
                    'min-height: 104.22px !important;' +
                    'flex-basis: 104.22px !important;' +
                    'border-radius: 7.28px !important;' +
                    'transform: translateY(0px) !important;' +
                '}' +

                /* Base transition on the icon so it scales smoothly */
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-icon {' +
                    'width: 66.53px !important;' +
                    'height: 66.53px !important;' +
                    'flex-basis: 66.53px !important;' +
                    'transition: transform 0.5s cubic-bezier(0.25, 1, 0.5, 1) !important;' +
                    'will-change: transform !important;' +
                    '-webkit-backface-visibility: hidden !important;' +
                    'backface-visibility: hidden !important;' +
                '}' +

                /* Target the icon when the parent button receives focus (split selectors for legacy CSS engines) */
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ':focus .mv-action-icon,' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + '.focused .mv-action-icon,' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + '.emby-button-focus .mv-action-icon {' +
                    'transform: scale(1.2) translateZ(0) !important;' +
                '}' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ':focus-visible .mv-action-icon {' +
                    'transform: scale(1.2) translateZ(0) !important;' +
                '}' +

                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-icon {' +
                    'width: 64px !important;' +
                    'height: 64px !important;' +
                    'flex-basis: 64px !important;' +
                '}' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ' .mv-action-icon svg {' +
                    'width: 66.53px !important;' +
                    'height: 66.53px !important;' +
                '}' +

                /* Tizen Button Focus background highlight (separated to prevent Tizen 3 CSS parser errors) */
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ':focus,' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + '.focused,' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + '.emby-button-focus {' +
                    'background: #00a4dc !important;' +
                    'color: #ffffff !important;' +
                    'border: 0 !important;' +
                    'box-shadow: none !important;' +
                    'transform: none;' +
                '}' +
                'body.mv-is-tizen .mainDetailButtons #' + CONFIG.actionButtonId + ':focus-visible {' +
                    'background: #00a4dc !important;' +
                    'color: #ffffff !important;' +
                    'border: 0 !important;' +
                    'box-shadow: none !important;' +
                    'transform: none;' +
                '}' +

                /* Tizen 3 Flexbox layout fallback for stats alignment */
                'body.mv-is-tizen .' + CONFIG.rootClass + ' .mv-stats {' +
                    'display: -webkit-box !important;' +
                    'display: -webkit-flex !important;' +
                    'display: flex !important;' +
                    '-webkit-box-pack: justify !important;' +
                    '-webkit-justify-content: space-between !important;' +
                    'justify-content: space-between !important;' +
                    '-webkit-box-align: center !important;' +
                    '-webkit-align-items: center !important;' +
                    'align-items: center !important;' +
                '}' +

                /* Disabled State */
                '.mainDetailButtons #' + CONFIG.actionButtonId + '[disabled] {' +
                    'opacity: .5;' +
                    'cursor: default;' +
                    'transform: none;' +
                    'box-shadow: none;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-message {' +
                    'color: rgba(255, 255, 255, .68);' +
                    'font-size: .88rem;' +
                    'line-height: 1.5;' +
                '}' +
                '.' + CONFIG.rootClass + ' .mv-error {' +
                    'margin-top: 10px;' +
                    'color: #ff9aae;' +
                    'font-size: .84rem;' +
                    'line-height: 1.45;' +
                    'overflow-wrap: anywhere;' +
                '}';

            document.head.appendChild(style);
        };

        var getOrCreatePanelHost = function(detailPage) {
            if (!detailPage) return null;
            var host = detailPage.querySelector('.' + CONFIG.rootClass);

            if (!host) {
                host = document.createElement('section');
                host.className = CONFIG.rootClass;
                host.id = CONFIG.panelId;
            }

            var primaryContent =
                detailPage.querySelector('.detailPagePrimaryContent') ||
                detailPage.querySelector('.detailPagePrimaryContentContainer') ||
                detailPage.querySelector('.detailPageContent');

            if (!primaryContent) {
                return null;
            }

            if (!host.isConnected) {
                var detailSection = primaryContent.querySelector('.detailSection');
                if (detailSection && detailSection.parentNode === primaryContent) {
                    primaryContent.insertBefore(host, detailSection);
                } else {
                    primaryContent.appendChild(host);
                }
            }

            return host;
        };

        var removePanelHost = function(detailPage) {
            var pages = detailPage ? [detailPage] : document.querySelectorAll('#itemDetailPage, .itemDetailPage, [data-role="page"].itemDetailPage, .' + CONFIG.rootClass);
            for (var i = 0; i < pages.length; i++) {
                var page = pages[i];
                if (page.classList && page.classList.contains(CONFIG.rootClass)) {
                    if (page.parentNode) {
                        page.parentNode.removeChild(page);
                    }
                } else {
                    var host = page.querySelector('.' + CONFIG.rootClass);
                    if (host && host.parentNode) {
                        host.parentNode.removeChild(host);
                    }
                }
            }
        };

        var getActionForStatus = function(status) {
            if (!status) {
                return null;
            }

            if (status.status === 'starting' || status.status === 'downloading') {
                return {
                    action: 'cancel',
                    label: 'Cancel Download',
                    className: 'mv-danger'
                };
            }

            if (status.status === 'not-downloaded' || status.status === 'cancelled' || status.status === 'failed') {
                return {
                    action: 'download',
                    label: 'Download to Server',
                    className: 'mv-primary'
                };
            }

            return {
                action: 'refresh',
                label: 'Refresh Status',
                className: ''
            };
        };

        var updateActionButton = function(button, status) {
            if (!button) {
                return;
            }

            var action = getActionForStatus(status);
            if (!action) {
                button.style.display = 'none';
                return;
            }

            button.style.display = 'inline-flex';
            button.setAttribute('data-mv-action', action.action);
            button.setAttribute('aria-label', action.label);

            var iconMarkup = action.action === 'cancel'
                ? '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9.5" fill="currentColor"/><path d="M8.5 8.5L15.5 15.5M15.5 8.5L8.5 15.5" fill="none" stroke="#1f1f1f" stroke-width="2.4" stroke-linecap="round"/></svg>'
                : '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M19.35 10.04A7.49 7.49 0 0 0 5.66 8.18 5.994 5.994 0 0 0 6 20h13a5 5 0 0 0 .35-9.96z" fill="currentColor"/><path d="M12 8.5v8m0 0-3-3m3 3 3-3" fill="none" stroke="#1f1f1f" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>';
            button.innerHTML = '<span class="mv-action-icon">' + iconMarkup + '</span>';

            button.classList.remove('mv-primary', 'mv-danger');
            if (action.className) {
                button.classList.add(action.className);
            }

            button.disabled = actionBusy;
            button.setAttribute('tabindex', '0');
            button.setAttribute('data-mv-focusable', 'true');
        };

        var ensureActionButton = function(detailPage, status) {
            var buttons = getMainDetailButtons(detailPage);
            if (!buttons) {
                return null;
            }

            var button = buttons.querySelector('#' + CONFIG.actionButtonId);
            if (!button) {
                button = document.createElement('button');
                button.className = 'emby-button mv-button';
                button.id = CONFIG.actionButtonId;
                button.type = 'button';
                button.setAttribute('tabindex', '0');
                button.setAttribute('data-mv-focusable', 'true');

                buttons.appendChild(button);
                log('Inserted Media Vault action button into .mainDetailButtons');
            }

            updateActionButton(button, status);
            return button;
        };

        var removeActionButton = function() {
            var button = document.getElementById(CONFIG.actionButtonId);
            if (button) {
                button.parentNode.removeChild(button);
                log('Removed Media Vault action button.');
            }
        };

        var statusLabel = function(status) {
            switch (status) {
                case 'starting':
                    return 'Preparing download';
                case 'downloading':
                    return 'Downloading to server';
                case 'complete':
                case 'cached':
                    return 'Cached on server';
                case 'cancelled':
                    return 'Download cancelled';
                case 'failed':
                    return 'Download failed';
                default:
                    return 'Not cached';
            }
        };

        var statusClass = function(status) {
            switch (status) {
                case 'starting':
                    return 'mv-starting';
                case 'downloading':
                    return 'mv-active';
                case 'complete':
                case 'cached':
                    return 'mv-complete';
                case 'cancelled':
                    return 'mv-cancelled';
                case 'failed':
                    return 'mv-failed';
                default:
                    return '';
            }
        };

        var isActiveStatus = function(status) {
            return status === 'starting' || status === 'downloading';
        };

        var isTerminalStatus = function(status) {
            return status === 'complete' || status === 'cached' || status === 'cancelled' || status === 'failed';
        };

        var renderLoading = function(host) {
            if (!host) return;
            host.innerHTML = '' +
                '<div class="mv-panel">' +
                    '<div class="mv-header">' +
                        '<span class="mv-cloud">' + CLOUD_SVG_HTML + '</span>' +
                        '<div>' +
                            '<div class="mv-title">Media Vault</div>' +
                            '<div class="mv-subtitle">Checking server cache…</div>' +
                        '</div>' +
                    '</div>' +
                    '<div class="mv-message">Loading download status…</div>' +
                '</div>';
        };

        var renderUnavailable = function(host) {
            if (!host) return;
            host.innerHTML = '' +
                '<div class="mv-panel">' +
                    '<div class="mv-header">' +
                        '<span class="mv-cloud">' + CLOUD_SVG_HTML + '</span>' +
                        '<div>' +
                            '<div class="mv-title">Media Vault</div>' +
                            '<div class="mv-subtitle">Download controls unavailable</div>' +
                        '</div>' +
                    '</div>' +
                    '<div class="mv-message">' +
                        'The Media Vault download service could not be reached. Normal Jellyfin playback is unaffected.' +
                    '</div>' +
                '</div>';
        };

        var renderStatus = function(host, status) {
            if (!host) return;
            var progress = Math.max(0, Math.min(100, Number(status.progress) || 0));
            var calculatedSpeed = getClientCalculatedSpeed(status);

            var etaSeconds = status.etaSeconds;
            if ((!isFinite(etaSeconds) || etaSeconds < 0) && calculatedSpeed > 0 && status.totalBytes > status.downloadedBytes) {
                etaSeconds = (status.totalBytes - status.downloadedBytes) / calculatedSpeed;
            }

            var runtimeCache = getRuntimeCacheMinutes(status);

            var active = isActiveStatus(status.status);
            var terminal = isTerminalStatus(status.status);

            var runtimeText = (runtimeCache !== null && isFinite(Number(status.runtimeMinutes)) && Number(status.runtimeMinutes) > 0)
                ? '≈ ' + formatRuntime(runtimeCache) + ' cached of ' + formatRuntime(status.runtimeMinutes)
                : 'Runtime cache estimate unavailable';

            var etaText = (active && isFinite(etaSeconds) && etaSeconds >= 0)
                ? 'ETA ' + formatDuration(etaSeconds)
                : (terminal ? '' : 'ETA —');

            var fileText = status.totalBytes > 0
                ? formatBytes(status.downloadedBytes) + ' / ' + formatBytes(status.totalBytes)
                : formatBytes(status.downloadedBytes) + ' / Size unknown';

            var speedText = active ? formatSpeed(calculatedSpeed) : '—';
            var progressText = (status.status === 'complete' || status.status === 'cached')
                ? '100.0%'
                : progress.toFixed(1) + '%';

            var errorHtml = status.error
                ? '<div class="mv-error">' + escapeHtml(status.error) + '</div>'
                : '';

            host.innerHTML = '' +
                '<div class="mv-panel" data-mv-status="' + escapeHtml(status.status) + '">' +
                    '<div class="mv-header">' +
                        '<span class="mv-cloud">' + CLOUD_SVG_HTML + '</span>' +
                        '<div>' +
                            '<div class="mv-title">Media Vault</div>' +
                            '<div class="mv-subtitle">' + escapeHtml(status.filename) + '</div>' +
                        '</div>' +
                    '</div>' +
                    '<div class="mv-status-row">' +
                        '<div class="mv-status ' + statusClass(status.status) + '">' +
                            escapeHtml(statusLabel(status.status)) +
                        '</div>' +
                        '<div class="mv-progress-label">' +
                            escapeHtml(progressText) +
                        '</div>' +
                    '</div>' +
                    '<div class="mv-progress" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="' + progress.toFixed(1) + '" aria-label="Media Vault download progress">' +
                        '<div class="mv-progress-fill" style="width:' + progress.toFixed(3) + '%"></div>' +
                    '</div>' +
                    '<div class="mv-stats">' +
                        '<div class="mv-stat"><strong>' + escapeHtml(fileText) + '</strong></div>' +
                        '<div class="mv-stat"></div>' +
                        '<div class="mv-stat"><strong>' + escapeHtml(speedText) + '</strong></div>' +
                    '</div>' +
                    '<div class="mv-runtime-row">' +
                        '<div>' +
                            '<strong>' + escapeHtml(runtimeText) + '</strong>' +
                        '</div>' +
                        '<div>' +
                            '<strong>' + escapeHtml(etaText) + '</strong>' +
                        '</div>' +
                    '</div>' +
                    errorHtml +
                '</div>';

            var detailPage = getVisibleDetailPage();
            if (detailPage) {
                ensureActionButton(detailPage, status);
            }

            lastStatus = {
                mediaVaultId: status.mediaVaultId,
                filename: status.filename,
                status: status.status,
                downloadedBytes: status.downloadedBytes,
                totalBytes: status.totalBytes,
                progress: status.progress,
                speedBytesPerSecond: status.speedBytesPerSecond,
                etaSeconds: status.etaSeconds,
                runtimeMinutes: status.runtimeMinutes,
                cachedRuntimeMinutes: status.cachedRuntimeMinutes,
                cachedRuntimePercentage: status.cachedRuntimePercentage,
                error: status.error,
                receivedAt: Date.now()
            };
        };

        var showActionError = function(host, error) {
            if (!host) return;
            var panel = host.querySelector('.mv-panel');
            if (!panel) {
                return;
            }

            var element = panel.querySelector('.mv-error');
            if (!element) {
                element = document.createElement('div');
                element.className = 'mv-error';
                panel.appendChild(element);
            }

            element.textContent = error instanceof Error ? error.message : String(error);
        };

        var startDownload = function(mediaVaultId, callback) {
            if (!mediaVaultId) {
                callback(new Error('Media Vault ID is missing.'));
                return;
            }

            fetchJson('/MediaVault/Downloads/start', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ MediaVaultId: mediaVaultId })
            }, callback);
        };

        var cancelDownload = function(mediaVaultId, callback) {
            if (!mediaVaultId) {
                callback(new Error('Media Vault ID is missing.'));
                return;
            }

            fetchJson('/MediaVault/Downloads/' + encodeURIComponent(mediaVaultId), {
                method: 'DELETE'
            }, callback);
        };

        var getDownloadStatus = function(mediaVaultId, callback) {
            fetchJson('/MediaVault/Downloads/' + encodeURIComponent(mediaVaultId), {}, callback);
        };

        var stopPolling = function() {
            if (pollTimer) {
                window.clearInterval(pollTimer);
                pollTimer = null;
            }
        };

        var refreshPanel = function(forceItemRefresh, callback) {
            callback = callback || function() {};
            var generation = renderGeneration;
            var detailPage = getVisibleDetailPage();

            if (!detailPage) {
                callback();
                return;
            }

            var itemId = getCurrentItemId();
            if (!itemId) {
                callback();
                return;
            }

            if (nonMediaVaultCache[itemId]) {
                removePanelHost(detailPage);
                removeActionButton();
                stopPolling();
                callback();
                return;
            }

            var itemChanged = itemId !== activeItemId;
            if (itemChanged) {
                activeItemId = itemId;
                activeMediaVaultId = null;
                activeItem = null;
                lastStatus = null;
                removeActionButton();
                removePanelHost(detailPage);
            }

            var processStatus = function() {
                if (!activeMediaVaultId) {
                    nonMediaVaultCache[itemId] = true;
                    removePanelHost(detailPage);
                    removeActionButton();
                    stopPolling();
                    callback();
                    return;
                }

                var host = getOrCreatePanelHost(detailPage);
                if (!host) {
                    callback();
                    return;
                }

                getDownloadStatus(activeMediaVaultId, function(err, rawStatus) {
                    if (generation !== renderGeneration || itemId !== getCurrentItemId()) {
                        callback();
                        return;
                    }

                    if (err) {
                        warn('Media Vault status refresh failed', err);
                        renderUnavailable(host);
                        callback();
                        return;
                    }

                    var status = normaliseStatus(rawStatus, getRuntimeMinutes(activeItem));
                    renderStatus(host, status);
                    callback();
                });
            };

            if (forceItemRefresh || !activeItem || itemChanged) {
                getJellyfinItem(itemId, function(err, item) {
                    if (err || generation !== renderGeneration || itemId !== getCurrentItemId()) {
                        if (err) {
                            warn('Could not identify Media Vault item', err);
                            removePanelHost(detailPage);
                            removeActionButton();
                        }
                        callback();
                        return;
                    }

                    activeItem = item;
                    var itemType = String(item && item.Type || '').toLowerCase();

                    // Completely disable/remove Media Vault panel for TV Series root pages, Seasons, BoxSets, and Folders
                    if (itemType === 'series' || itemType === 'season' || itemType === 'boxset' || itemType === 'folder') {
                        nonMediaVaultCache[itemId] = true;
                        removePanelHost(detailPage);
                        removeActionButton();
                        stopPolling();
                        callback();
                        return;
                    }

                    activeMediaVaultId = getMediaVaultId(activeItem);

                    if (!activeMediaVaultId) {
                        findMediaVaultIdFromCatalogue(activeItem, function(errCat, idFromCat) {
                            if (generation !== renderGeneration || itemId !== getCurrentItemId()) {
                                callback();
                                return;
                            }

                            activeMediaVaultId = idFromCat;
                            if (!activeMediaVaultId) {
                                nonMediaVaultCache[itemId] = true;
                                removePanelHost(detailPage);
                                removeActionButton();
                                stopPolling();
                                log('Current item is not a Media Vault item.');
                                callback();
                                return;
                            }
                            processStatus();
                        });
                    } else {
                        processStatus();
                    }
                });
            } else {
                processStatus();
            }
        };

        var startPolling = function() {
            stopPolling();

            pollTimer = window.setInterval(function() {
                removeTestBanner();
                var detailPage = getVisibleDetailPage();

                if (detailPage && getCurrentItemId() && activeMediaVaultId) {
                    refreshPanel(false);
                    ensureActionButton(detailPage, lastStatus);
                } else if (!detailPage) {
                    stopPolling();
                }
            }, CONFIG.pollIntervalMs);
        };

        var initialiseForCurrentPage = function() {
            renderGeneration += 1;
            var generation = renderGeneration;

            activeItemId = null;
            activeMediaVaultId = null;
            activeItem = null;
            lastStatus = null;
            actionBusy = false;

            var detailPage = getVisibleDetailPage();
            var itemId = getCurrentItemId();

            if (itemId && nonMediaVaultCache[itemId]) {
                removeActionButton();
                removePanelHost(detailPage);
                stopPolling();
                removeTestBanner();
                return;
            }

            removeActionButton();
            removePanelHost(detailPage);
            stopPolling();
            removeTestBanner();

            var startedAt = Date.now();
            var hadDetailPage = false;

            var checkStep = function() {
                if (generation !== renderGeneration) {
                    return;
                }

                var currentDetailPage = getVisibleDetailPage();
                var currentItemId = getCurrentItemId();

                if (currentItemId && nonMediaVaultCache[currentItemId]) {
                    removeActionButton();
                    removePanelHost(currentDetailPage);
                    stopPolling();
                    return;
                }

                if (currentDetailPage && currentItemId) {
                    hadDetailPage = true;

                    refreshPanel(true, function() {
                        if (generation !== renderGeneration) {
                            return;
                        }

                        if (activeMediaVaultId) {
                            ensureActionButton(currentDetailPage, lastStatus);
                            startPolling();
                            return;
                        }

                        if (activeItem && activeItemId === currentItemId) {
                            return;
                        }

                        if (Date.now() - startedAt < CONFIG.detailPageWaitMs) {
                            window.setTimeout(checkStep, 200);
                        }
                    });
                } else if (hadDetailPage && !currentDetailPage) {
                    return;
                } else {
                    if (Date.now() - startedAt < CONFIG.detailPageWaitMs) {
                        window.setTimeout(checkStep, 200);
                    }
                }
            };

            checkStep();
        };

        var scheduleNavigationRefresh = function() {
            window.clearTimeout(navigationTimer);

            navigationTimer = window.setTimeout(function() {
                initialiseForCurrentPage();
            }, CONFIG.mutationDebounceMs);
        };

        var handleNavigation = function() {
            scheduleNavigationRefresh();
        };

        var installActionHandler = function() {
            document.addEventListener('click', function(event) {
                var target = event.target;
                if (!target || !target.closest) {
                    return;
                }

                var button = target.closest('#' + CONFIG.actionButtonId);
                if (!button) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();

                if (actionBusy || button.disabled) {
                    return;
                }

                var actionName = button.getAttribute('data-mv-action');
                if (!actionName) {
                    return;
                }

                log('ACTION BUTTON ACTIVATED:', actionName);
                actionBusy = true;
                button.disabled = true;

                var onComplete = function(err) {
                    if (err) {
                        warn('Media Vault action failed', err);
                        var detailPage = getVisibleDetailPage();
                        var host = detailPage ? detailPage.querySelector('.' + CONFIG.rootClass) : null;
                        if (host) {
                            showActionError(host, err);
                        }
                    }

                    refreshPanel(true, function() {
                        actionBusy = false;
                        var currentDetail = getVisibleDetailPage();
                        if (currentDetail && activeMediaVaultId) {
                            ensureActionButton(currentDetail, lastStatus);
                        }
                    });
                };

                if (actionName === 'download') {
                    startDownload(activeMediaVaultId, onComplete);
                } else if (actionName === 'cancel') {
                    cancelDownload(activeMediaVaultId, onComplete);
                } else {
                    onComplete(null);
                }
            }, true);

            document.addEventListener('keydown', function(event) {
                var target = event.target;
                if (!target || !target.closest) {
                    return;
                }

                var button = target.closest('#' + CONFIG.actionButtonId);
                if (!button) {
                    return;
                }

                if (event.key === 'Enter' || event.key === ' ' || event.key === 'Spacebar') {
                    event.preventDefault();
                    event.stopPropagation();
                    button.click();
                }
            }, true);

            log('Installed delegated Media Vault action handler.');
        };

        var installNavigationHooks = function() {
            window.addEventListener('hashchange', handleNavigation);
            window.addEventListener('popstate', handleNavigation);
            window.addEventListener('viewshow', handleNavigation);

            var originalPushState = history.pushState;
            var originalReplaceState = history.replaceState;

            if (originalPushState) {
                history.pushState = function() {
                    var result = originalPushState.apply(this, arguments);
                    handleNavigation();
                    return result;
                };
            }

            if (originalReplaceState) {
                history.replaceState = function() {
                    var result = originalReplaceState.apply(this, arguments);
                    handleNavigation();
                    return result;
                };
            }
        };

        var installMutationObserver = function() {
            if (typeof MutationObserver === 'undefined') {
                return;
            }

            if (mutationObserver) {
                mutationObserver.disconnect();
            }

            mutationObserver = new MutationObserver(function(mutations) {
                var relevant = false;

                for (var i = 0; i < mutations.length; i++) {
                    var mut = mutations[i];
                    if (mut.type === 'childList') {
                        var isSelfMutation = false;
                        var nodes = Array.prototype.slice.call(mut.addedNodes).concat(Array.prototype.slice.call(mut.removedNodes));
                        for (var j = 0; j < nodes.length; j++) {
                            var node = nodes[j];
                            if (node.nodeType === 1) {
                                if (
                                    (node.classList && node.classList.contains(CONFIG.rootClass)) ||
                                    node.id === CONFIG.actionButtonId ||
                                    node.id === CONFIG.panelId
                                ) {
                                    isSelfMutation = true;
                                    break;
                                }
                            }
                        }

                        if (!isSelfMutation) {
                            relevant = true;
                            break;
                        }
                    }
                }

                if (!relevant) {
                    return;
                }

                window.clearTimeout(mutationTimer);

                mutationTimer = window.setTimeout(function() {
                    removeTestBanner();
                    var detailPage = getVisibleDetailPage();

                    if (!detailPage) {
                        return;
                    }

                    var itemId = getCurrentItemId();

                    if (!itemId) {
                        return;
                    }

                    if (nonMediaVaultCache[itemId]) {
                        return;
                    }

                    if (activeMediaVaultId && activeItemId === itemId) {
                        ensureActionButton(detailPage, lastStatus);
                        return;
                    }

                    scheduleNavigationRefresh();
                }, CONFIG.mutationDebounceMs);
            });

            var target = document.querySelector('.mainAnimatedPages') || document.body;

            if (!target) {
                return;
            }

            mutationObserver.observe(target, {
                childList: true,
                subtree: true
            });

            log('Installed detail-page MutationObserver.');
        };

        var boot = function() {
            if (isTizenDevice() && document.body) {
                document.body.classList.add('mv-is-tizen');
            }
            createStyles();
            installActionHandler();
            installNavigationHooks();
            installMutationObserver();
            removeTestBanner();

            window.setTimeout(function() {
                initialiseForCurrentPage();
            }, CONFIG.startupDelayMs);

            log('Media Vault Jellyfin Web UI loaded (' + CONFIG.buildId + ')');
        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', boot, false);
        } else {
            boot();
        }
    } catch (e) {
        if (console && console.error) {
            console.error('Error in Injected JavaScript ["Media Vault"]:', e);
        }
    }
})();
} catch (e) { console.error('Error in Injected JavaScript ["Media Vault"]:', e); } })();
} catch (e) { console.error('Error in Injected JavaScript ["Media Vault"]:', e); } })();

} catch (e) { console.error('Error in Injected JavaScript ["Media Vault"]:', e); } })();
/* User Script: Idk */
(function() { try {
const script = document.createElement("script");
script.src = `https://cdn.jsdelivr.net/gh/ranaldsgift/KefinTweaks@latest/kefinTweaks-plugin.js`;
script.async = true;
document.head.appendChild(script);
} catch (e) { console.error('Error in Injected JavaScript ["Idk"]:', e); } })();
/* User Script: KefinTweaks-Config */
(function() { try {
// KefinTweaks Configuration
// This file is automatically generated by KefinTweaks Configuration UI
// Do not edit manually unless you know what you're doing

window.KefinTweaksConfig = {
  "kefinTweaksRoot": "https://cdn.jsdelivr.net/gh/ranaldsgift/KefinTweaks@latest/",
  "scripts": {
    "watchlist": true,
    "homeScreen": true,
    "search": true,
    "infiniteScroll": true,
    "removeContinue": true,
    "skinManager": true,
    "headerTabs": true,
    "customMenuLinks": true,
    "breadcrumbs": true,
    "playlist": true,
    "itemDetailsCollections": true,
    "flattenSingleSeasonShows": true,
    "seriesInfo": true,
    "collections": true,
    "subtitleSearch": true,
    "exclusiveElsewhere": true,
    "backdropLeakFix": true,
    "dashboardButtonFix": true
  },
  "homeScreen": {
    "defaultItemLimit": 16,
    "defaultSortOrder": "Random",
    "defaultCardFormat": "Poster",
    "recentlyReleased": {
      "enabled": true,
      "movies": {
        "enabled": true,
        "itemLimit": 16,
        "name": "Recently Released Movies",
        "sortOrder": "Random",
        "sortOrderDirection": "Descending",
        "cardFormat": "Poster",
        "order": 30,
        "isPlayed": null,
        "minAgeInDays": 0,
        "maxAgeInDays": 30
      },
      "episodes": {
        "enabled": true,
        "itemLimit": 16,
        "name": "Recently Aired Episodes",
        "sortOrder": "PremiereDate",
        "sortOrderDirection": "Descending",
        "cardFormat": "Backdrop",
        "order": 31,
        "isPlayed": null,
        "minAgeInDays": 0,
        "maxAgeInDays": 30
      }
    },
    "recentlyAddedInLibrary": {
      "3907e680d89cc51e9fc20fbd10b1e877": {
        "enabled": false,
        "itemLimit": 30,
        "name": "Recently Added in Local Movies",
        "cardFormat": "Poster",
        "order": 11
      },
      "ca2af7d3d667e71233f310f0ace0cc88": {
        "enabled": false,
        "itemLimit": 30,
        "name": "Recently Added in Local Shows",
        "cardFormat": "Thumb",
        "order": 11
      }
    },
    "trending": {
      "enabled": false,
      "itemLimit": 16,
      "name": "Trending",
      "sortOrder": "Random",
      "sortOrderDirection": "Ascending",
      "cardFormat": "Poster",
      "order": 32,
      "isPlayed": null
    },
    "popularTVNetworks": {
      "enabled": false,
      "minimumShowsForNetwork": 5,
      "itemLimit": 16,
      "name": "Popular TV Networks",
      "sortOrder": "Random",
      "sortOrderDirection": "Ascending",
      "cardFormat": "Poster",
      "order": 61
    },
    "watchlist": {
      "enabled": false,
      "itemLimit": 16,
      "name": "Watchlist",
      "sortOrder": "DateAdded",
      "sortOrderDirection": "Descending",
      "cardFormat": "Poster",
      "order": 60
    },
    "watchAgain": {
      "enabled": false,
      "itemLimit": 16,
      "name": "Watch Again",
      "sortOrder": "Random",
      "sortOrderDirection": "Ascending",
      "cardFormat": "Poster",
      "order": 62
    },
    "upcoming": {
      "enabled": false,
      "itemLimit": 48,
      "name": "Upcoming",
      "cardFormat": "Thumb",
      "order": 20
    },
    "imdbTop250": {
      "enabled": false,
      "itemLimit": 16,
      "name": "IMDb Top 250",
      "sortOrder": "Random",
      "sortOrderDirection": "Ascending",
      "cardFormat": "Poster",
      "order": 21,
      "isPlayed": null
    },
    "seasonal": {
      "enabled": false,
      "enableSeasonalAnimations": true,
      "enableSeasonalBackground": true,
      "defaultItemLimit": 16,
      "defaultSortOrder": "Random",
      "defaultCardFormat": "Poster",
      "seasons": [
        {
          "id": "halloween",
          "name": "Halloween",
          "enabled": true,
          "startDate": "10-01",
          "endDate": "10-31",
          "order": 100,
          "sections": [
            {
              "id": "halloween-tag",
              "enabled": true,
              "name": "Halloween Movies",
              "type": "Tag",
              "source": "halloween",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Poster",
              "order": 50,
              "renderMode": "Normal",
              "spotlight": false,
              "discoveryEnabled": false,
              "searchTerm": "",
              "includeItemTypes": [
                "Movie"
              ],
              "additionalQueryOptions": []
            },
            {
              "id": "halloween-horror",
              "enabled": true,
              "name": "Horror Genre",
              "type": "Genre",
              "source": "Horror",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Poster",
              "order": 51
            },
            {
              "id": "halloween-thriller",
              "enabled": true,
              "name": "Thriller Genre",
              "type": "Genre",
              "source": "Thriller",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Poster",
              "order": 52
            }
          ]
        },
        {
          "id": "thanksgiving",
          "name": "Thanksgiving",
          "enabled": true,
          "startDate": "11-20",
          "endDate": "11-30",
          "order": 50,
          "sections": [
            {
              "id": "seasonal-2-section-0",
              "enabled": true,
              "name": "Thanksgiving Movies",
              "type": "Tag",
              "source": "thanksgiving",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Poster",
              "order": 50,
              "renderMode": "Spotlight",
              "spotlight": true,
              "discoveryEnabled": false,
              "searchTerm": "",
              "includeItemTypes": [
                "Movie"
              ],
              "additionalQueryOptions": []
            },
            {
              "id": "seasonal-2-section-1",
              "enabled": true,
              "name": "Thanksgiving Episodes",
              "type": "Parent",
              "source": "",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Thumb",
              "order": 51,
              "renderMode": "Normal",
              "spotlight": false,
              "discoveryEnabled": false,
              "searchTerm": "thanksgiving",
              "includeItemTypes": [
                "Episode"
              ],
              "additionalQueryOptions": []
            }
          ]
        },
        {
          "id": "christmas",
          "name": "Christmas",
          "enabled": true,
          "startDate": "12-01",
          "endDate": "12-31",
          "order": 100,
          "sections": [
            {
              "id": "seasonal-1-section-0",
              "enabled": true,
              "name": "Christmas Movies",
              "type": "Tag",
              "source": "christmas",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Poster",
              "order": 50,
              "renderMode": "Spotlight",
              "spotlight": true,
              "discoveryEnabled": false,
              "searchTerm": "",
              "includeItemTypes": [
                "Movie"
              ],
              "additionalQueryOptions": []
            },
            {
              "id": "seasonal-1-section-1",
              "enabled": true,
              "name": "Christmas Episodes",
              "type": "Parent",
              "source": "",
              "itemLimit": 16,
              "sortOrder": "Random",
              "sortOrderDirection": "Ascending",
              "cardFormat": "Thumb",
              "order": 51,
              "renderMode": "Normal",
              "spotlight": false,
              "discoveryEnabled": false,
              "searchTerm": "christmas",
              "includeItemTypes": [
                "Episode"
              ],
              "additionalQueryOptions": []
            }
          ]
        }
      ]
    },
    "discovery": {
      "enabled": false,
      "infiniteScroll": true,
      "randomizeOrder": false,
      "minPeopleAppearances": 10,
      "minGenreMovieCount": 50,
      "spotlightDiscoveryChance": 0.5,
      "renderSpotlightAboveMatching": false,
      "defaultItemLimit": 16,
      "defaultSortOrder": "Random",
      "defaultCardFormat": "Poster",
      "sectionTypes": {
        "spotlightGenre": {
          "itemLimit": 16,
          "name": "Top Rated [Genre] Movies",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "spotlightNetwork": {
          "itemLimit": 16,
          "name": "Top Rated Shows from [Studio]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "genreMovies": {
          "itemLimit": 16,
          "name": "[Genre] Movies",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "studioShows": {
          "itemLimit": 16,
          "name": "Shows from [Studio]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "collections": {
          "itemLimit": 16,
          "name": "[Collection Name]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true,
          "minimumItems": 10
        },
        "becauseYouWatched": {
          "itemLimit": 16,
          "name": "Because you watched [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "becauseYouLiked": {
          "itemLimit": 16,
          "name": "Because you liked [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "starringTopActor": {
          "itemLimit": 16,
          "name": "Starring [Actor]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "directedByTopDirector": {
          "itemLimit": 16,
          "name": "Directed by [Director]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "writtenByTopWriter": {
          "itemLimit": 16,
          "name": "Written by [Writer]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "becauseYouRecentlyWatched": {
          "itemLimit": 16,
          "name": "Because you recently watched [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "starringActorRecentlyWatched": {
          "itemLimit": 16,
          "name": "Starring [Actor] because you recently watched [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "directedByDirectorRecentlyWatched": {
          "itemLimit": 16,
          "name": "Directed by [Director] because you recently watched [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        },
        "writtenByWriterRecentlyWatched": {
          "itemLimit": 16,
          "name": "Written by [Writer] because you recently watched [Movie]",
          "cardFormat": "Poster",
          "order": 100,
          "isPlayed": null,
          "enabled": true
        }
      }
    },
    "customSections": []
  },
  "exclusiveElsewhere": {
    "hideServerName": true
  },
  "search": {
    "enableJellyseerr": false
  },
  "defaultSkin": "Ultrachromic",
  "skins": [],
  "themes": [],
  "customMenuLinks": [],
  "enabled": true,
  "flattenSingleSeasonShows": {
    "hideSingleSeasonContainer": false
  },
  "optionalIncludes": [
    {
      "key": "global-lscambo13-custom-media-covers-latest-min.css",
      "enabled": true
    },
    {
      "key": "global-JamsRepos-central-libraries-small.css",
      "enabled": true
    },
    {
      "key": "global-JamsRepos-hide-my-media.css",
      "enabled": true
    },
    {
      "key": "global-JamsRepos-smaller-cast.css",
      "enabled": false
    },
    {
      "key": "global-LitCastVlog-ScyFlow-EpisodeGrid.css",
      "enabled": true
    },
    {
      "key": "global-LitCastVlog-ScyFLow-RoundCastCrew.css",
      "enabled": false
    },
    {
      "key": "global-CTalvio-smallercast.css",
      "enabled": false
    },
    {
      "key": "global-CTalvio-episodes_compactlist.css",
      "enabled": false
    },
    {
      "key": "global-CTalvio-episodes_grid.css",
      "enabled": false
    },
    {
      "key": "global-CTalvio-pan-animation.css",
      "enabled": true
    },
    {
      "key": "global-LitCastVlog-ScyFlow-AnimatedOverlay.css",
      "enabled": true
    }
  ]
};
} catch (e) { console.error('Error in Injected JavaScript ["KefinTweaks-Config"]:', e); } })();
