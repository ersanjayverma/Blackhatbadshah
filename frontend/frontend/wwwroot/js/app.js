window.scrollChatToBottom = () => {
    const el = document.querySelector('.chat-body');
    if (el) el.scrollTop = el.scrollHeight;
};
window.downloadFile = (fileName, contentType, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();

    URL.revokeObjectURL(url);
};
