// File download utility for reports
window.downloadFile = function(filename, mimeType, fileBytes) {
    const blob = new Blob([new Uint8Array(fileBytes)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};
