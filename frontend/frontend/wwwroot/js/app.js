window.scrollChatToBottom = () => {
    const el = document.querySelector('.chat-body');
    if (el) el.scrollTop = el.scrollHeight;
};