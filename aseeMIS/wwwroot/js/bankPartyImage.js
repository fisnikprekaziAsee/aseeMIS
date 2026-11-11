// bankPartyImage.js — Syncfusion ImageEditor integration for BankParty profile / signature / document images.
//
// Architecture (async-first, no save-button coupling):
//   • Paths come SERVER-PREFILLED in hidden inputs (#bpiPath_1..5) — no GET round-trip on load.
//   • Profile (1), Signature (2), Document Front (3), Document Back (4), Proof of Address (5)
//     thumbs render directly via
//     <img src="/Common/CRUD/Preview?path=..."> when a path exists.
//   • Apply (crop) and Delete go to /Common/CRUD/Add or /Common/CRUD/Update
//     on BankPartyUploadDocuments (Form 102). UI slots map to type ids 1-6.
//   • Requests are SERIALISED per BankPartyId via a tiny in-page queue so concurrent crops/deletes
//     never race each other or the server-side rename-on-overwrite.
//   • A single overlay (#bpiBusyOverlay) covers all four cards while any request is in flight.
//   • On success the hidden input is updated with the new path returned by the server (or cleared
//     on delete) so subsequent uploads always carry the latest state.
//   • On failure the local dirty bit stays set, the canvas keeps the latest crop, and we toast
//     the error — the user can retry without losing what they cropped.

var currentWrapper = null;
var bpiPaths = { 1: null, 2: null, 3: null, 4: null, 5: null }; // UI slot paths
var bpiDocs = {}; // UploadDocumentTypeId -> { id, path }
var bpiModalMovedToBody = false;
var bpiTypeNames = {
    1: 'Anë e Përparme e Letërnjoftimit',
    2: 'Anë e Pasme e Letërnjoftimit',
    3: 'Dokumenti i Pasaportës',
    4: 'Imazhi i Nënshkrimit',
    5: 'Fotografia e Profilit',
    6: 'Dëshmi e Adresës',
    7: 'Signicat biometric face image'
};

// Per-BankPartyId queue. We only ever have one in this page, but keeping the structure
// makes it trivial to lift this script into a multi-party context later.
var bpiQueues = {};
var bpiInFlight = 0;

// Move #bpiCropModal to <body> AFTER the Syncfusion ImageEditor has initialised
// (called from bpiEditorCreated). The modal MUST live at body level so any ancestor
// with transform/filter/perspective doesn't break its `position: fixed` overlay —
// that's why earlier you saw it stuck at the top of the page instead of centered.
// We DON'T move it before init or we lose the editor reference (= "Editor not initialised").
function bpiMoveModalToBodyIfNeeded() {
    if (bpiModalMovedToBody) return;
    var modal = document.getElementById('bpiCropModal');
    if (modal && modal.parentElement !== document.body) {
        document.body.appendChild(modal);
        bpiModalMovedToBody = true;
    }
}

// ── Init ─────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {

    document.getElementById('bpiCropClose').addEventListener('click', bpiHideModal);
    document.getElementById('bpiCropBackdrop').addEventListener('click', bpiHideModal);
    document.getElementById('bpiBtnRotate').addEventListener('click', bpiRotate);
    var zoomInBtn = document.getElementById('bpiBtnZoomIn');
    var zoomOutBtn = document.getElementById('bpiBtnZoomOut');
    if (zoomInBtn) zoomInBtn.addEventListener('click', function () { bpiZoom(0.15); });
    if (zoomOutBtn) zoomOutBtn.addEventListener('click', function () { bpiZoom(-0.15); });
    document.getElementById('bpiBtnReset').addEventListener('click', bpiReset);
    document.getElementById('bpiBtnCancel').addEventListener('click', bpiHideModal);
    document.getElementById('bpiBtnApply').addEventListener('click', bpiApply);

    document.getElementById('bpiBtnOpen').addEventListener('click', function () {
        document.getElementById('bpiFileInput').click();
    });

    document.getElementById('bpiFileInput').addEventListener('change', function (e) {
        if (!e.target.files.length) return;
        var url = URL.createObjectURL(e.target.files[0]);
        e.target.value = null;
        var ie = getEditor();
        if (ie) ie.open(url);
    });

    var bankPartyIdEl = document.getElementById('bpiBankPartyId');
    var bankPartyId = parseInt(bankPartyIdEl ? bankPartyIdEl.value : '0');
    if (!bankPartyId) return;

    // Read server-prefilled paths from hidden inputs (no AJAX needed).
    try { bpiDocs = JSON.parse(readHidden('bpiPhotoDocuments') || '{}') || {}; }
    catch (_) { bpiDocs = {}; }

    bpiPaths[1] = readHidden('bpiPath_1');
    bpiPaths[2] = readHidden('bpiPath_2');
    bpiPaths[3] = readHidden('bpiPath_3');
    bpiPaths[4] = readHidden('bpiPath_4');
    bpiPaths[5] = readHidden('bpiPath_5');

    // Render the two always-visible thumbs straight from the DB-stored path.
    // Doc front/back are rendered by bpiHandleDocType once the doc type is chosen.
    if (bpiPaths[1]) renderThumb(1, bpiPaths[1]);
    if (bpiPaths[2]) renderThumb(2, bpiPaths[2]);
    if (bpiPaths[5]) renderThumb(5, bpiPaths[5]);

    var initialDocType = readHidden('bpiInitialDocType');
    if (initialDocType) {
        var sel = document.getElementById('bpiDocType');
        if (sel) {
            sel.value = initialDocType;
            bpiHandleDocType();
        }
    }
});

function readHidden(id) {
    var el = document.getElementById(id);
    var v = el ? (el.value || '').trim() : '';
    return v || null;
}

function writeHidden(id, value) {
    var el = document.getElementById(id);
    if (el) el.value = value || '';
}

// ── Build a browser URL for a stored path (encryption + storage-aware) ───────
//    Paths persisted by the [FileUpload] framework are RELATIVE to
//    {ContentRoot}/SecureStorage (e.g. "1/B_000001/115/P/115_profile.jpg" or
//    "1/B_000001/115/P/115_front.bin"). These are NOT served as public assets —
//    they must be streamed through /Common/CRUD/Preview which transparently decrypts
//    ".bin" files using the active FileSecurity key and returns image bytes with the
//    right content-type.
//
//    Only paths that are already absolute (start with "/" or "http(s)://") are
//    rendered directly — those are legacy/public assets under wwwroot.
function imageUrlFromPath(path) {
    if (!path) return null;
    var s = String(path).trim();
    if (!s) return null;
    if (/^https?:\/\//i.test(s) || s.charAt(0) === '/') return s;
    return '/Common/CRUD/Preview?path=' + encodeURIComponent(s);
}

// ── Render the server image into the thumb canvas of the wrapper ─────────────
// Canvas is sized to the source aspect ratio (capped at MAX) and CSS lets it
// shrink to fit the zone with `object-fit: contain`. This avoids the squished /
// blurry look of the previous "always 64x64 stretched to 100%" approach.
function renderThumb(typeId, path) {
    if (!path) return;
    var wrapper = document.querySelector('.doc-image-wrapper[data-type="' + typeId + '"]');
    if (!wrapper) return;

    // Cache-buster appended so when the path is the SAME (stable filename, OverwriteExisting=true)
    // the browser still sees a different URL and refetches — otherwise a cropped re-upload would
    // show the old image until the user hard-refreshes.
    var url = imageUrlFromPath(path);
    if (url && url.indexOf('?') >= 0) url += '&_=' + Date.now();
    else if (url) url += '?_=' + Date.now();

    var canvas = wrapper.querySelector('.img-canvas');
    var img = new Image();
    img.onload = function () {
        var MAX = 256;
        var w = img.naturalWidth || img.width || MAX;
        var h = img.naturalHeight || img.height || MAX;
        var scale = Math.min(1, MAX / Math.max(w, h));
        canvas.width = Math.round(w * scale);
        canvas.height = Math.round(h * scale);
        var ctx = canvas.getContext('2d');
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        canvas.style.borderRadius = (typeId === 1) ? '50%' : '4px';
        wrapper.classList.remove('e-hide');
        try { wrapper.dataset.base64 = canvas.toDataURL('image/jpeg', 0.92); } catch (_) { /* keep empty */ }
    };
    img.onerror = function () {
        console.warn('bpi: thumb failed type', typeId, 'url', url);
    };
    img.src = url;
}

// ── Syncfusion callbacks ─────────────────────────────────────────────────────
function bpiEditorCreated() {
    var ie = getEditor();
    if (ie) ie.update();
    bpiMoveModalToBodyIfNeeded();
    var editorEl = document.getElementById('bpiImageEditor');
    if (editorEl && !editorEl.dataset.bpiWheelBound) {
        editorEl.dataset.bpiWheelBound = '1';
        editorEl.addEventListener('wheel', function (e) {
            if (!document.getElementById('bpiCropModal')?.classList.contains('bpi-open')) return;
            e.preventDefault();
            bpiZoom(e.deltaY < 0 ? 0.1 : -0.1);
        }, { passive: false });
    }
}

function bpiEditorOpened() {
    if (!currentWrapper) return;
    var ie = getEditor();
    if (!ie) return;
    var type = parseInt(currentWrapper.dataset.type);
    try {
        switch (type) {
            case 1: ie.select('circle'); break;
            case 2: ie.select('custom', { width: 300, height: 100 }); break;
            case 3: ie.select('custom', { width: 400, height: 250 }); break;
            case 4: ie.select('custom', { width: 350, height: 250 }); break;
            default: ie.select('square');
        }
    } catch (e) { console.warn('bpi: select() failed', e); }
}

// ── Modal show/hide ──────────────────────────────────────────────────────────
function bpiShowModal() {
    var modal = document.getElementById('bpiCropModal');
    modal.classList.add('bpi-open');

    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            var ie = getEditor();
            if (!ie) return;
            try { ie.update(); } catch (_) { }

            var type = currentWrapper ? parseInt(currentWrapper.dataset.type, 10) : 0;
            var existing = (currentWrapper && currentWrapper.dataset.base64)
                || (type && bpiPaths[type])
                || null;
            var url = existing && String(existing).indexOf('data:') === 0
                ? existing
                : imageUrlFromPath(existing);

            if (url) {
                try { ie.open(url); } catch (e) { console.warn('bpi: open existing failed', e); }
            } else {
                try { ie.reset(); } catch (_) { }
            }
        });
    });
}

function bpiHideModal() {
    document.getElementById('bpiCropModal').classList.remove('bpi-open');
}

// ── Open editor on zone click (the whole .bpi-zone is now the single hit-target) ──
// IMPORTANT: the delete-chip is a child of the zone and has its OWN handler that
// stops propagation, so clicking ✕ removes the image without re-opening the editor.
document.addEventListener('click', function (e) {
    // If the click landed inside the delete chip, let its dedicated handler run.
    if (e.target.closest('.bpi-delete-chip')) return;

    var wrapper = e.target.closest('.doc-image-wrapper');
    if (!wrapper) return;

    currentWrapper = wrapper;
    bpiShowModal();
});

function bpiRotate() {
    var ie = getEditor();
    if (ie) { try { ie.rotate(-90); } catch (e) { console.warn('bpi: rotate failed', e); } }
}

function bpiZoom(delta) {
    var ie = getEditor();
    if (!ie) return;
    try {
        if (typeof ie.zoom === 'function') ie.zoom(delta);
        else if (delta > 0 && typeof ie.zoomIn === 'function') ie.zoomIn();
        else if (delta < 0 && typeof ie.zoomOut === 'function') ie.zoomOut();
    } catch (e) { console.warn('bpi: zoom failed', e); }
}

function bpiReset() {
    var ie = getEditor();
    if (ie) { try { ie.reset(); ie.open(''); } catch (e) { console.warn('bpi: reset failed', e); } }
}

// ── APPLY ────────────────────────────────────────────────────────────────────
// Crops in the editor, captures the result as base64, then ENQUEUES an UPDATE to
// /Common/CRUD/Update. The UI closes the modal immediately and the thumb shows
// the local crop straight away — the network round-trip is hidden behind the
// spinner overlay so the user perceives the change as instant.
function bpiApply() {
    if (!currentWrapper) {
        notify?.warning?.('No target zone selected.');
        return;
    }
    var ie = getEditor();
    if (!ie) {
        notify?.error?.('Editor not initialised.');
        return;
    }

    try { ie.crop(); } catch (e) { console.warn('bpi: crop() threw', e); }

    requestAnimationFrame(function () {
        setTimeout(captureAndUpload, 30);
    });
}

function captureAndUpload() {
    var dataUrl = tryCaptureDataUrl();
    if (!dataUrl) {
        notify?.error?.('Could not capture cropped image. Reset and try again.');
        return;
    }

    var type = parseInt(currentWrapper.dataset.type);
    renderLocalThumb(currentWrapper, type, dataUrl);
    currentWrapper.dataset.base64 = dataUrl;
    currentWrapper.dataset.dirty = '1';
    bpiHideModal();

    enqueue(function (done) {
        sendDocumentChange(type, dataUrl, /*clear*/ false, function (ok, newPath, msg) {
            if (ok) {
                bpiPaths[type] = newPath || null;
                writeHidden('bpiPath_' + type, newPath || '');
                currentWrapper.dataset.dirty = '';
                notify?.success?.('Saved.');
            } else {
                notify?.error?.(msg || 'Save failed.');
            }
            done();
        });
    });
}

// Local-only redraw of the cropped result into the wrapper's canvas — used right
// after capture and after a successful upload, so the thumb reflects the latest
// state without waiting for the server round-trip.
function renderLocalThumb(wrapper, type, dataUrl) {
    var out = wrapper.querySelector('.img-canvas');
    var img = new Image();
    img.onload = function () {
        var MAX = 256;
        var w = img.naturalWidth || img.width || MAX;
        var h = img.naturalHeight || img.height || MAX;
        var scale = Math.min(1, MAX / Math.max(w, h));
        out.width = Math.round(w * scale);
        out.height = Math.round(h * scale);
        var ctx = out.getContext('2d');
        ctx.imageSmoothingQuality = 'high';
        ctx.clearRect(0, 0, out.width, out.height);
        ctx.drawImage(img, 0, 0, out.width, out.height);
        out.style.borderRadius = (type === 1) ? '50%' : '4px';
        wrapper.classList.remove('e-hide');
    };
    img.onerror = function () {
        console.error('bpi: local thumb failed for type', type);
    };
    img.src = dataUrl;
}

function tryCaptureDataUrl() {
    var ie = getEditor();

    // Method 1 — Syncfusion ImageData → temp canvas → dataURL
    try {
        var imgData = ie.getImageData();
        if (imgData && imgData.width > 0 && imgData.height > 0) {
            var tmp = document.createElement('canvas');
            tmp.width = imgData.width;
            tmp.height = imgData.height;
            tmp.getContext('2d').putImageData(imgData, 0, 0);
            var url = tmp.toDataURL('image/jpeg', 0.92);
            tmp.remove();
            if (url && url.length > 500) return url;
        }
    } catch (e) {
        console.warn('bpi: method1 threw', e);
    }

    // Method 2 — scan the editor's child <canvas> elements (largest non-empty wins)
    var editorEl = document.getElementById('bpiImageEditor');
    if (!editorEl) return null;

    var canvases = editorEl.querySelectorAll('canvas');
    var best = null;
    var bestSize = 0;

    for (var i = 0; i < canvases.length; i++) {
        var c = canvases[i];
        if (!c.width || !c.height) continue;
        try {
            var url2 = c.toDataURL('image/jpeg', 0.92);
            if (url2 && url2.length > 500) {
                var sz = c.width * c.height;
                if (sz > bestSize) { best = url2; bestSize = sz; }
            }
        } catch (e) {
            console.warn('bpi: canvas', i, 'toDataURL threw', e);
        }
    }
    return best;
}

// ── REMOVE (async via /Common/CRUD/Update with the slot cleared) ─────────────
// Bound in CAPTURE phase + stopPropagation so the surrounding .bpi-zone click
// handler (which opens the editor) does NOT also fire when the user hits the chip.
document.addEventListener('click', function (e) {
    var btn = e.target.closest('.delete-doc');
    if (!btn) return;

    e.stopPropagation();
    e.preventDefault();

    var type = parseInt(btn.dataset.type);
    var wrapper = document.querySelector('.doc-image-wrapper[data-type="' + type + '"]');
    if (!wrapper) return;

    // Optimistic UI: clear the canvas locally so the user sees instant feedback.
    var canvas = wrapper.querySelector('.img-canvas');
    if (canvas) {
        canvas.getContext('2d').clearRect(0, 0, canvas.width || 64, canvas.height || 64);
        canvas.width = 0; canvas.height = 0;
    }
    wrapper.dataset.base64 = '';
    wrapper.classList.add('e-hide');

    enqueue(function (done) {
        sendDocumentChange(type, /*base64*/ null, /*clear*/ true, function (ok, _newPath, msg) {
            if (ok) {
                bpiPaths[type] = null;
                writeHidden('bpiPath_' + type, '');
                wrapper.dataset.dirty = '';
                notify?.success?.('Removed.');
            } else {
                wrapper.dataset.dirty = '1';
                if (bpiPaths[type]) renderThumb(type, bpiPaths[type]);
                notify?.error?.(msg || 'Remove failed.');
            }
            done();
        });
    });
}, true);

// ── Doc type selector: toggles slot visibility AND loads document thumbs.
function bpiHandleDocType() {
    var val = document.getElementById('bpiDocType').value;
    var placeholder = document.getElementById('bpiDocPlaceholder');
    var frontWrap = document.getElementById('bpiDocFrontWrap');
    var backWrap = document.getElementById('bpiDocBackWrap');
    var frontLabel = document.getElementById('bpiDocFrontLabel');

    if (!val) {
        placeholder.style.display = '';
        frontWrap.style.display = 'none';
        backWrap.style.display = 'none';
        return;
    }

    placeholder.style.display = 'none';
    frontWrap.style.display = '';
    frontLabel.textContent = val === 'passport' ? 'Photo / biographical page' : 'Front — photo side';
    backWrap.style.display = val === 'passport' ? 'none' : '';

    if (bpiPaths[3]) renderThumb(3, bpiPaths[3]);
    if (bpiPaths[4] && val !== 'passport') renderThumb(4, bpiPaths[4]);
}

function bpiDocTypeValue() {
    var sel = document.getElementById('bpiDocType');
    return sel ? (sel.value || '') : '';
}

function uploadTypeForSlot(slot) {
    if (slot === 1) return 5;
    if (slot === 2) return 4;
    if (slot === 5) return 6;
    if (slot === 4) return 2;
    if (slot === 3) return bpiDocTypeValue() === 'passport' ? 3 : 1;
    return 0;
}

function rememberDoc(typeId, id, path) {
    bpiDocs[String(typeId)] = { id: id || 0, path: path || '' };
    writeHidden('bpiPhotoDocuments', JSON.stringify(bpiDocs));
}

function forgetDoc(typeId) {
    delete bpiDocs[String(typeId)];
    writeHidden('bpiPhotoDocuments', JSON.stringify(bpiDocs));
}

function existingDoc(typeId) {
    return bpiDocs[String(typeId)] || null;
}

function buildDocumentPayload(typeId, existingId, base64DataUrl, clear) {
    var bankPartyId = parseInt(readHidden('bpiBankPartyId') || '0');
    var payload = {
        UploadDocumentId: existingId || 0,
        BankPartyId: bankPartyId,
        UploadDocumentTypeId: typeId,
        DocumentName: bpiTypeNames[typeId] || ('Document ' + typeId),
        DocumentDescription: bpiTypeNames[typeId] || '',
        IsPrimary: true,
        IsActive: true,
        IsApproved: true,
        DocumentFilePath: clear ? '' : ((existingDoc(typeId) || {}).path || '')
    };

    if (!clear && base64DataUrl) {
        payload.DocumentFilePathBase64 = base64DataUrl;
        payload.DocumentFileOriginalFileName = 'crop_' + Date.now() + '.jpg';
        payload.DocumentFileContentType = 'image/jpeg';
    }

    return payload;
}

function sendDocumentChange(slot, base64DataUrl, clear, cb) {
    var sessionId = readHidden('bpiImageFormSessionId');
    if (!sessionId) {
        cb(false, null, 'Document form session not available (Form 102 access denied).');
        return;
    }

    var typeId = uploadTypeForSlot(slot);
    if (!typeId) {
        cb(false, null, 'Select a document type first.');
        return;
    }

    var current = existingDoc(typeId);
    var existingId = current && current.id ? Number(current.id) : 0;
    if (clear && !existingId) {
        cb(true, '', null);
        return;
    }

    var endpoint = existingId ? '/Common/CRUD/Update' : '/Common/CRUD/Add';
    var payload = buildDocumentPayload(typeId, existingId, base64DataUrl, clear);

    showBusy(true);
    fetch(endpoint, {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'FormSessionId': sessionId
        },
        body: JSON.stringify(payload)
    })
    .then(function (r) { return r.json().catch(function () { return { success: false, message: 'Bad server response.' }; }); })
    .then(function (res) {
        showBusy(false);
        if (!res || res.success !== true) {
            cb(false, null, (res && res.message) || 'Server rejected the change.');
            return;
        }
        var updated = res.updatedRow || {};
        rememberDoc(typeId, updated.UploadDocumentId || existingId, clear ? '' : (updated.DocumentFilePath || ''));
        cb(true, clear ? '' : (updated.DocumentFilePath || ''), null);
    })
    .catch(function (err) {
        showBusy(false);
        cb(false, null, (err && err.message) || 'Network error.');
    });
}

// ── Queue ────────────────────────────────────────────────────────────────────
// One job at a time, in FIFO order, so two quick crops or a crop-then-delete
// can't race the server-side archive-on-overwrite (which renames the previous
// file BEFORE writing the new one — a concurrent second upload could otherwise
// archive the file we just wrote).
function enqueue(job) {
    var bankPartyId = parseInt(readHidden('bpiBankPartyId') || '0');
    if (!bpiQueues[bankPartyId]) bpiQueues[bankPartyId] = [];
    bpiQueues[bankPartyId].push(job);
    drainQueue(bankPartyId);
}

function drainQueue(bankPartyId) {
    if (bpiInFlight > 0) return;
    var q = bpiQueues[bankPartyId];
    if (!q || q.length === 0) return;
    var job = q.shift();
    bpiInFlight++;
    job(function () {
        bpiInFlight--;
        drainQueue(bankPartyId);
    });
}

function showBusy(on) {
    var ov = document.getElementById('bpiBusyOverlay');
    if (!ov) return;
    if (on) ov.removeAttribute('hidden');
    else ov.setAttribute('hidden', '');
}

// ── Helpers ───────────────────────────────────────────────────────────────────
// Robust editor lookup: try the EJ2 instance array first (always populated after init),
// then fall back to ej.base.getComponent with both casings (Syncfusion EJ2 has used
// both 'imageeditor' and 'ImageEditor' across versions).
function getEditor() {
    var el = document.getElementById('bpiImageEditor');
    if (!el) return null;

    if (el.ej2_instances && el.ej2_instances.length > 0) {
        return el.ej2_instances[0];
    }

    if (window.ej && ej.base && ej.base.getComponent) {
        return ej.base.getComponent(el, 'imageeditor')
            || ej.base.getComponent(el, 'ImageEditor')
            || null;
    }

    return null;
}
