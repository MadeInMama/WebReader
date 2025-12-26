(async () => {
    // === CONFIG ===
    const selector = 'img'; // ← Modify: e.g., 'article img', '.post img'
    const minDimension = 50; // skip tiny icons (px)
    const scrollDelay = 3000; // ms to wait after scrolling

    // === 1. Auto-scroll to load lazy images ===
    console.log('⏬ Scrolling to load all images...');
    let scrolled = 0;
    const scrollInterval = setInterval(() => {
        window.scrollBy(0, 100);
        scrolled += 300;
        console.log(scrolled + " | " + document.querySelector('.article_layer__content').scrollHeight * 1.5);
        if (scrolled > document.querySelector('.article_layer__content').scrollHeight * 1.5) clearInterval(scrollInterval);
    }, 100);
    await new Promise(r => setTimeout(r, document.querySelector('.article_layer__content').scrollHeight * 1.5 / 3));
    clearInterval(scrollInterval);
    window.scrollTo(0, 0);
    await new Promise(r => setTimeout(r, 800));

    // === 2. Collect images ===
    const imgs = Array.from(document.querySelector('.article_layer__content').querySelectorAll('img'))
        .filter(img =>
            img.src &&
            img.naturalWidth > 0 &&
            img.offsetHeight > 0 && !img.classList.contains('article_layer__content_footer_icon_bg'));

    if (imgs.length === 0) return alert(`❌ No images found for "${selector}"`);

    console.log(`📦 Found ${imgs.length} images. Creating ZIP...`);

    // === 3. Load JSZip & FileSaver (CSP-safe via Blob) ===
    const loadScript = async (url) => {
        const res = await fetch(url);
        const code = await res.text();
        const blob = new Blob([code], {type: 'application/javascript'});
        return new Promise((resolve, reject) => {
            const s = document.createElement('script');
            s.src = URL.createObjectURL(blob);
            s.onload = () => {
                URL.revokeObjectURL(s.src);
                resolve();
            };
            s.onerror = reject;
            document.head.appendChild(s);
        });
    };

    try {
        await loadScript('https://cdn.jsdelivr.net/npm/jszip@3.10.1/dist/jszip.min.js');
        await loadScript('https://cdn.jsdelivr.net/npm/file-saver@2.0.5/dist/FileSaver.min.js');
    } catch (e) {
        return alert('❌ Failed to load libraries. Check network/console.');
    }

    // === 4. Create ZIP ===
    const zip = new JSZip();
    let successCount = 0;

    for (let i = 0; i < imgs.length; i++) {
        const img = imgs[i];
        try {
            // Get clean filename
            let filename = img.alt?.trim() ||
                img.title?.trim() ||
                img.src.split('/').pop().split('?')[0] ||
                `image_${String(i + 1).padStart(3, '0')}`;

            // Sanitize filename
            filename = filename.replace(/[<>:"/\\|?*]/g, '_');
            if (!/\.\w{3,4}$/.test(filename)) filename += '.jpg';

            // Fetch image (with CORS retry)
            let blob;
            try {
                const res = await fetch(img.src, {mode: 'cors'});
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                blob = await res.blob();
            } catch (err) {
                // Fallback: use <img> data URL (if same-origin or canvas not tainted)
                const canvas = document.createElement('canvas');
                const ctx = canvas.getContext('2d');
                canvas.width = img.naturalWidth;
                canvas.height = img.naturalHeight;
                ctx.drawImage(img, 0, 0);
                const dataUrl = canvas.toDataURL('image/png');
                const res = await fetch(dataUrl);
                blob = await res.blob();
            }

            zip.file(filename, blob);
            successCount++;
            console.log(`✅ ${filename}`);
        } catch (e) {
            console.warn(`⚠️ Skip ${img.src}:`, e.message);
        }
    }

    // === 5. Save ZIP ===
    if (successCount === 0) return alert('❌ No images could be downloaded.');

    console.log(`⏳ Generating ZIP...`);
    const content = await zip.generateAsync({
        type: 'blob',
        compression: 'DEFLATE',
        compressionOptions: {level: 6}
    });

    // Save using FileSaver.js
    saveAs(content, `${document.title?.replace(/[^\w\s-]/g, '_') || 'images'}_${successCount}imgs.zip`);
    console.log(`🎉 ZIP saved with ${successCount}/${imgs.length} images!`);
})();
