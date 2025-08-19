window.drawFrame = async (cameraId, streamRef) => {
    const arrayBuffer = await streamRef.arrayBuffer();
    let blob = new Blob([arrayBuffer], { type: "image/jpeg" });
    let url = URL.createObjectURL(blob);

    let img = new Image();
    img.onload = function () {
        let canvas = document.getElementById(cameraId);
        if (!canvas) return;

        let ctx = canvas.getContext("2d");
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        URL.revokeObjectURL(url);
    };
    img.src = url;
};