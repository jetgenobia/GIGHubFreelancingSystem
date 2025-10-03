// Project Chat JavaScript
let connection;
let currentChatRoomId;
let currentUserId;
let isConnectionReady = false;
let typingTimer;
let connectionAttempts = 0;
const maxConnectionAttempts = 3;

// Initialize chat functionality
document.addEventListener('DOMContentLoaded', function () {
    initializeChat();

    // Scroll to bottom on page load to show latest messages
    setTimeout(() => {
        scrollToBottom();
    }, 200);
});

function initializeChat() {
    currentChatRoomId = document.getElementById('currentChatRoomId')?.value;
    currentUserId = document.getElementById('currentUserId')?.value;
    const targetUserId = document.getElementById('targetUserId')?.value;

    console.log('Chat initialization:', {
        currentChatRoomId,
        currentUserId,
        targetUserId,
        hasCurrentUserIdElement: !!document.getElementById('currentUserId'),
        hasChatRoomIdElement: !!document.getElementById('currentChatRoomId')
    });

    if (!currentUserId) {
        console.log('Chat not initialized - no current user ID');
        return;
    }

    // Initialize SignalR connection with better reconnection handling
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();

    setupSignalRHandlers();
    startConnection();
    setupChatEventListeners();

    // If we have a target user ID but no chat room ID, we're starting a new chat
    if (targetUserId && (!currentChatRoomId || currentChatRoomId === '00000000-0000-0000-0000-000000000000')) {
        currentChatRoomId = 'new';
        console.log('Setting up new chat with target user:', targetUserId);
    }

    // Scroll to bottom on initial load to show latest messages
    setTimeout(() => {
        scrollToBottom();
    }, 100);
}

function setupSignalRHandlers() {
    // Connection events
    connection.on('Connected', (connectionId) => {
        console.log('Connected with ID:', connectionId);
        isConnectionReady = true;
        if (currentChatRoomId && currentChatRoomId !== 'new') {
            connection.invoke('JoinChatRoom', currentChatRoomId);
        }
    });

    connection.on('JoinedRoom', (roomName) => {
        console.log('Joined room:', roomName);
    });

    connection.on('ChatRoomCreated', (newChatRoomId) => {
        console.log('New chat room created:', newChatRoomId);
        currentChatRoomId = newChatRoomId;
        // Update the hidden input field
        const chatRoomIdInput = document.getElementById('currentChatRoomId');
        if (chatRoomIdInput) {
            chatRoomIdInput.value = newChatRoomId;
        }
        // Join the new room
        connection.invoke('JoinChatRoom', newChatRoomId);

        // Update the sidebar with the new chat room and navigate to it
        updateSidebarWithNewChatAndNavigate(newChatRoomId);
    });

    connection.on('Error', (error) => {
        console.error('SignalR Error:', error);
        // Only show critical errors, not routine access denied messages
        if (!error.includes('Access denied') && !error.includes('chat room not found')) {
            showError('Connection error: ' + error);
        }
    });

    // Message events
    connection.on('ReceiveMessage', (message) => {
        console.log('Received message:', message);

        if (!message || (!message.Message && !message.message)) {
            console.error('Message is missing content:', message);
            return;
        }

        addMessageToChat(message);
        updateChatList(message);
    });

    connection.on('ReceiveFile', (fileMessage) => {
        console.log('Received file message:', fileMessage);

        if (!fileMessage || (!fileMessage.FileName && !fileMessage.fileName)) {
            console.error('File message is missing content:', fileMessage);
            return;
        }

        addFileMessageToChat(fileMessage);
        updateChatList(fileMessage);
    });

    // Typing events
    connection.on('UserTyping', (userId, isTyping) => {
        if (userId !== currentUserId) {
            showTypingIndicator(isTyping);
        }
    });

    // Read receipts
    connection.on('MessagesRead', (userId) => {
        if (userId !== currentUserId) {
            markMessagesAsRead();
        }
    });

    // Video call events - handled by global notification system
    connection.on('IncomingVideoCall', (callData) => {
        console.log('Incoming video call:', callData);
        // Global notification system will handle this
    });

    connection.on('CallRequested', (callData) => {
        console.log('Call requested:', callData);
        // Show the waiting notification for the caller
        showCallWaitingNotification();

        // **CRITICAL FIX**: Do NOT prepare video call URL here if ChatRoomId is undefined
        // The ChatRoomId might be undefined at this stage
        console.log('Call initiated, waiting for acceptance. ChatRoomId:', callData.ChatRoomId);

        // Do NOT set window.pendingVideoCallUrl here - it will be handled in CallAccepted
    });

    connection.on('CallAccepted', (callData) => {
        console.log('Call accepted:', callData);
        // Hide the waiting notification
        hideCallWaitingNotification();

        // **CRITICAL FIX**: Use camelCase property names (chatRoomId, not ChatRoomId)
        let videoCallUrl;

        if (callData.chatRoomId && callData.chatRoomId !== 'undefined') {
            // Use the actual chat room ID from the accepted call data
            videoCallUrl = `/Chat/VideoCall?chatRoomId=${callData.chatRoomId}`;
            console.log('Opening video call window for caller after acceptance:', videoCallUrl);
            window.open(videoCallUrl, 'VideoCall', 'width=800,height=600,scrollbars=no,resizable=yes');
        } else if (callData.isTemporary) { // Also check camelCase
            // Handle temporary chat room case
            const targetUserId = document.getElementById('targetUserId')?.value;
            if (targetUserId) {
                videoCallUrl = `/Chat/VideoCall?targetUserId=${targetUserId}`;
                console.log('Opening video call window for temporary chat:', videoCallUrl);
                window.open(videoCallUrl, 'VideoCall', 'width=800,height=600,scrollbars=no,resizable=yes');
            } else {
                console.error('No target user ID available for temporary chat');
            }
        } else {
            console.error('No valid chatRoomId in CallAccepted data:', callData);
        }
    });

    connection.on('CallDeclined', (callData) => {
        console.log('Call declined:', callData);
        // Hide the waiting notification
        hideCallWaitingNotification();

        // Clear any pending video call URL
        if (window.pendingVideoCallUrl) {
            window.pendingVideoCallUrl = null;
        }
    });

    connection.on('VideoCallEnded', (callData) => {
        console.log('Video call ended:', callData);
        // Global notification system will handle this
    });

    // Enhanced connection state handling
    connection.onclose((error) => {
        console.log('SignalR connection closed', error);
        isConnectionReady = false;

        // Don't auto-reconnect if user is navigating away
        if (!window.isUnloading) {
            setTimeout(() => {
                if (connectionAttempts < maxConnectionAttempts) {
                    console.log('Attempting to reconnect...');
                    startConnection();
                }
            }, 5000);
        }
    });

    connection.onreconnecting((error) => {
        console.log('SignalR reconnecting...', error);
        isConnectionReady = false;
    });

    connection.onreconnected((connectionId) => {
        console.log('SignalR reconnected with ID:', connectionId);
        isConnectionReady = true;
        connectionAttempts = 0;

        // Rejoin current chat room
        if (currentChatRoomId && currentChatRoomId !== 'new') {
            connection.invoke('JoinChatRoom', currentChatRoomId);
        }
    });
}

function startConnection() {
    connectionAttempts++;

    connection.start().then(() => {
        console.log('SignalR Connected');
        isConnectionReady = true;
        connectionAttempts = 0; // Reset on successful connection

        if (currentChatRoomId && currentChatRoomId !== 'new') {
            connection.invoke('JoinChatRoom', currentChatRoomId);
        }
    }).catch(err => {
        console.error('SignalR Connection Error: ', err);
        isConnectionReady = false;

        if (connectionAttempts < maxConnectionAttempts) {
            console.log(`Retrying connection... Attempt ${connectionAttempts + 1} of ${maxConnectionAttempts}`);
            setTimeout(() => {
                startConnection();
            }, 2000 * connectionAttempts); // Exponential backoff
        } else {
            console.error('Max connection attempts reached');
            showError('Unable to connect to the server. Please refresh the page.');
        }
    });
}

// Add this to handle page unload
window.addEventListener('beforeunload', () => {
    window.isUnloading = true;
});

function setupChatEventListeners() {
    const messageForm = document.getElementById('messageForm');
    const messageInput = document.getElementById('messageInput');
    const fileInput = document.getElementById('fileInput');

    if (messageForm) {
        messageForm.addEventListener('submit', handleMessageSubmit);
    }

    if (messageInput) {
        messageInput.addEventListener('input', handleTyping);
        messageInput.addEventListener('keydown', handleKeyDown);
    }

    if (fileInput) {
        fileInput.addEventListener('change', handleFileSelect);
    }

    // Chat list item clicks
    document.querySelectorAll('.chat-item').forEach(item => {
        item.addEventListener('click', function () {
            const chatRoomId = this.dataset.chatRoomId;
            if (chatRoomId) {
                window.location.href = `/Chat/Index?chatRoomId=${chatRoomId}`;
            }
        });
    });
}

function handleMessageSubmit(e) {
    e.preventDefault();

    const messageInput = document.getElementById('messageInput');
    const message = messageInput.value.trim();

    if (!message || !isConnectionReady || !currentChatRoomId) {
        console.log('Message submission blocked:', { message: !!message, isConnectionReady, currentChatRoomId });
        return;
    }

    console.log('Sending message:', { message, currentChatRoomId, isConnectionReady });
    const targetUserId = document.getElementById('targetUserId')?.value;

    // Send message via SignalR
    if (currentChatRoomId === 'new' && targetUserId) {
        // Creating a new chat room
        console.log('Sending new chat message:', { message, targetUserId });
        connection.invoke('SendMessage', 'new', message, 'text', targetUserId)
            .then(() => {
                console.log('Message sent successfully (new chat)');
                messageInput.value = '';
                // Stop typing indicator
                if (currentChatRoomId !== 'new') {
                    connection.invoke('Typing', currentChatRoomId, false);
                }
            })
            .catch(err => {
                console.error('Failed to send message:', err);
                showError('Failed to send message: ' + err.message);
            });
    } else {
        // Existing chat room
        console.log('Sending existing chat message:', { message, currentChatRoomId });
        connection.invoke('SendMessage', currentChatRoomId, message, 'text', null)
            .then(() => {
                console.log('Message sent successfully (existing chat)');
                messageInput.value = '';
                // Stop typing indicator
                connection.invoke('Typing', currentChatRoomId, false);
            })
            .catch(err => {
                console.error('Failed to send message:', err);
                showError('Failed to send message: ' + err.message);
            });
    }
}

function handleFileSelect(e) {
    const file = e.target.files[0];
    if (!file) return;

    uploadFile(file);
}

function uploadFile(file) {
    const formData = new FormData();
    formData.append('file', file);

    // For new chats, we need to handle file upload differently
    if (currentChatRoomId === 'new') {
        showError('Please send a text message first to create the chat room before uploading files.');
        document.getElementById('fileInput').value = '';
        return;
    }

    formData.append('chatRoomId', currentChatRoomId);

    // Show loading message
    const loadingMessage = addLoadingMessage('Uploading file...');

    fetch(`/Chat/UploadFile?chatRoomId=${currentChatRoomId}`, {
        method: 'POST',
        body: formData
    })
        .then(response => response.json())
        .then(response => {
            loadingMessage.remove();

            if (response.success) {
                // Send file via SignalR
                connection.invoke('SendFile', currentChatRoomId, response.fileName,
                    response.fileUrl, response.fileSize, response.fileType)
                    .then(() => {
                        console.log('File sent successfully');
                    })
                    .catch(err => {
                        console.error('Failed to send file:', err);
                        showError('Failed to send file: ' + err.message);
                    });
            } else {
                showError('Upload failed: ' + response.message);
            }
        })
        .catch(error => {
            loadingMessage.remove();
            console.error('Upload error:', error);
            showError('Upload failed: ' + error.message);
        });

    // Clear file input
    document.getElementById('fileInput').value = '';
}

function handleTyping() {
    if (!isConnectionReady || !currentChatRoomId || currentChatRoomId === 'new') return;

    // Clear existing timer
    clearTimeout(typingTimer);

    // Send typing indicator
    connection.invoke('Typing', currentChatRoomId, true);

    // Set timer to stop typing indicator
    typingTimer = setTimeout(() => {
        connection.invoke('Typing', currentChatRoomId, false);
    }, 1000);
}

function handleKeyDown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleMessageSubmit(e);
    }
}

function addMessageToChat(message) {
    const messagesContainer = document.getElementById('messagesContainer');
    if (!messagesContainer) return;

    // Validate message object and provide fallbacks
    if (!message || typeof message !== 'object') {
        console.error('Invalid message object received:', message);
        return;
    }

    const messageDiv = document.createElement('div');
    // Compare sender ID as strings to handle both GUID and string formats
    const isOwnMessage = String(message.SenderId || message.senderId) === String(currentUserId);

    console.log('Message ownership check:', {
        messageSenderId: message.SenderId || message.senderId,
        currentUserId: currentUserId,
        isOwnMessage: isOwnMessage
    });

    messageDiv.className = `message ${isOwnMessage ? 'own' : ''}`;
    messageDiv.dataset.messageId = message.Id || message.id || 'temp-' + Date.now();

    const timeDisplay = formatTime(message.SentAt || message.sentAt);
    const messageContent = message.Message || message.message || 'Message unavailable';
    const senderName = message.SenderName || message.senderName || 'Unknown User';

    messageDiv.innerHTML = `
    <div class="message-bubble">
        <div class="message-content">${escapeHtml(messageContent)}</div>
        <div class="message-time" data-timestamp="${message.SentAt || message.sentAt}">
            ${timeDisplay}
            ${isOwnMessage ? '<i class="fas fa-check" title="Sent"></i>' : ''}
        </div>
    </div>
`;

    messagesContainer.appendChild(messageDiv);
    scrollToBottom();
}

function addFileMessageToChat(fileMessage) {
    const messagesContainer = document.getElementById('messagesContainer');
    if (!messagesContainer) return;

    // Validate file message object and provide fallbacks
    if (!fileMessage || typeof fileMessage !== 'object') {
        console.error('Invalid file message object received:', fileMessage);
        return;
    }

    const messageDiv = document.createElement('div');
    // Compare sender ID as strings to handle both GUID and string formats
    const isOwnMessage = String(fileMessage.SenderId || fileMessage.senderId) === String(currentUserId);

    messageDiv.className = `message ${isOwnMessage ? 'own' : ''}`;
    messageDiv.dataset.messageId = fileMessage.Id || fileMessage.id || 'temp-' + Date.now();

    const timeDisplay = formatTime(fileMessage.SentAt || fileMessage.sentAt);
    const fileSize = fileMessage.FileSize || fileMessage.fileSize || 0;
    const fileSizeFormatted = fileSize > 0 ? (fileSize / 1024 / 1024).toFixed(2) + ' MB' : '';
    const fileName = fileMessage.FileName || fileMessage.fileName || 'Unknown File';
    const fileUrl = fileMessage.FileUrl || fileMessage.fileUrl || '#';
    const fileType = fileMessage.FileType || fileMessage.fileType || '';
    const senderName = fileMessage.SenderName || fileMessage.senderName || 'Unknown User';

    // Determine if it's an image based on message type or file type
    const isImage = fileMessage.MessageType === 'image' ||
        (fileType && fileType.startsWith('image/')) ||
        fileName.toLowerCase().match(/\.(jpg|jpeg|png|gif|webp)$/);

    const isVideo = fileMessage.MessageType === 'video' ||
        (fileType && fileType.startsWith('video/')) ||
        fileName.toLowerCase().match(/\.(mp4|mov|avi|wmv)$/);

    const linkClass = isOwnMessage ? 'hover:underline' : 'hover:underline';

    let contentHtml = '';

    if (isImage) {
        // For images, show only the image without filename or size
        contentHtml = `
            <div class="image-message">
                <img src="${fileUrl}" alt="${fileName}"
                     class="max-w rounded-lg cursor-pointer"
                     onclick="openImageModal('${fileUrl}')"
                     style="max-height: 300px; object-fit: cover;">
            </div>
        `;
    } else if (isVideo) {
        contentHtml = `
            <div class="video-message">
                <video class="max-w-xs rounded-lg" controls style="max-height: 200px;">
                    <source src="${fileUrl}" type="${fileType}">
                    Your browser does not support the video tag.
                </video>
                <div class="text-xs opacity-75 mt-1">${fileName}</div>
                ${fileSizeFormatted ? `<div class="text-xs opacity-75">${fileSizeFormatted}</div>` : ''}
            </div>
        `;
    } else {
        contentHtml = `
            <div class="file-message">
                <div class="file-icon">
                    <svg class="w-[24px] h-[24px] text-gray-800" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="currentColor" viewBox="0 0 24 24">
                        <path fill-rule="evenodd" d="M9 2.221V7H4.221a2 2 0 0 1 .365-.5L8.5 2.586A2 2 0 0 1 9 2.22ZM11 2v5a2 2 0 0 1-2 2H4v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2h-7Z" clip-rule="evenodd" />
                    </svg>
                </div>
                <div>
                    <a href="${fileUrl}" download="${fileName}" class="${linkClass}">
                        ${fileName}
                    </a>
                    ${fileSizeFormatted ? `<div class="text-xs opacity-75">${fileSizeFormatted}</div>` : ''}
                </div>
            </div>
        `;
    }

    messageDiv.innerHTML = `
    <div class="message-bubble">
        ${contentHtml}
        <div class="message-time" data-timestamp="${fileMessage.SentAt || fileMessage.sentAt}">
            ${timeDisplay}
            ${isOwnMessage ? '<i class="fas fa-check" title="Sent"></i>' : ''}
        </div>
    </div>
`;

    messagesContainer.appendChild(messageDiv);
    scrollToBottom();
}

function addLoadingMessage(text) {
    const messagesContainer = document.getElementById('messagesContainer');
    if (!messagesContainer) return null;

    const loadingDiv = document.createElement('div');
    loadingDiv.className = 'message loading-message';
    loadingDiv.innerHTML = `
        <div class="message-bubble">
            <div class="message-content">
                <i class="fas fa-spinner fa-spin"></i> ${text}
            </div>
        </div>
    `;

    messagesContainer.appendChild(loadingDiv);
    scrollToBottom();
    return loadingDiv;
}

function showTypingIndicator(isTyping) {
    const typingIndicator = document.getElementById('typingIndicator');
    if (typingIndicator) {
        typingIndicator.style.display = isTyping ? 'block' : 'none';
        if (isTyping) {
            scrollToBottom();
        }
    }
}

function markMessagesAsRead() {
    if (!isConnectionReady || !currentChatRoomId || currentChatRoomId === 'new') return;

    connection.invoke('MarkAsRead', currentChatRoomId)
        .catch(err => {
            console.error('Failed to mark messages as read:', err);
        });
}

function updateChatList(message) {
    // Update the chat list item with the latest message
    if (currentChatRoomId === 'new') return; // Don't update list for new chats until room is created

    const chatItem = document.querySelector(`[data-chat-room-id="${currentChatRoomId}"]`);
    if (chatItem) {
        const messageElement = chatItem.querySelector('.chat-item-message');
        if (messageElement) {
            let messageText = '';

            // Check if it's a file/image/video message
            if (message.MessageType === 'file' || message.MessageType === 'image' || message.MessageType === 'video' ||
                message.FileName || message.fileName || message.FileUrl || message.fileUrl) {
                messageText = 'Sent an attachment';
            } else {
                // Regular text message
                messageText = message.Message || message.message || 'New message';
            }

            messageElement.textContent = messageText;
        }

        const timeElement = chatItem.querySelector('.chat-item-time');
        if (timeElement && (message.SentAt || message.sentAt)) {
            timeElement.textContent = formatTime(message.SentAt || message.sentAt);
        }
    }
}

function updateSidebarWithNewChatAndNavigate(newChatRoomId) {
    // Fetch updated chat list from server
    fetch('/Chat/GetChatList')
        .then(response => response.json())
        .then(data => {
            if (data.success && data.chatList) {
                updateSidebarChatList(data.chatList);

                // Navigate to the new chat room after a short delay to ensure sidebar is updated
                setTimeout(() => {
                    window.location.href = `/Chat/Index?chatRoomId=${newChatRoomId}`;
                }, 500);
            }
        })
        .catch(error => {
            console.error('Failed to update sidebar:', error);
            // Still navigate even if sidebar update fails
            setTimeout(() => {
                window.location.href = `/Chat/Index?chatRoomId=${newChatRoomId}`;
            }, 500);
        });
}

function updateSidebarChatList(chatList) {
    const sidebar = document.getElementById('chat-sidebar');
    if (!sidebar) return;

    if (chatList.length === 0) {
        // Show "No Active Chats" message
        sidebar.innerHTML = `
            <h2 class="text-2xl font-bold pl-7">Chats</h2>
            <div class="no-chats p-5">
                <i class="fas fa-comments"></i>
                <h3 class="text-lg font-medium mb-2">No Active Chats</h3>
                <p class="text-sm">You don't have any active conversations yet.</p>
            </div>
        `;
        return;
    }

    // Build the new chat list HTML
    let chatListHTML = '<h2 class="text-2xl font-bold pl-7">Chats</h2>';

    chatList.forEach(chat => {
        // For new chat rooms, we want to mark the newly created one as active
        const isActive = chat.ChatRoomId === currentChatRoomId;
        const partnerPhoto = chat.Partner.Photo || 'https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497';
        const partnerName = `${chat.Partner.FirstName} ${chat.Partner.LastName}`;
        const lastMessageTime = formatTime(chat.LastMessageTime);

        chatListHTML += `
            <div class="chat-item ${isActive ? 'active' : ''}" data-chat-room-id="${chat.ChatRoomId}">
                <div class="chat-item-header">
                    <div class="flex items-center space-x-3">
                        <img class="rounded-full" style="width: 50px; height:auto"
                             src="${partnerPhoto}"
                             alt="${chat.Partner.FirstName}">
                        <div>
                            <div class="chat-item-name">${partnerName}</div>
                            <div class="chat-item-message text-truncate">${chat.LastMessage}</div>
                        </div>
                    </div>
                    <div class="flex flex-col items-end">
                        <div class="chat-item-time">${lastMessageTime}</div>
                        ${chat.UnreadCount > 0 ? `<div class="unread-badge">${chat.UnreadCount}</div>` : ''}
                    </div>
                </div>
            </div>
        `;
    });

    // Update the sidebar content
    sidebar.innerHTML = chatListHTML;

    // Re-attach click event listeners to the new chat items
    document.querySelectorAll('.chat-item').forEach(item => {
        item.addEventListener('click', function () {
            const chatRoomId = this.dataset.chatRoomId;
            if (chatRoomId) {
                window.location.href = `/Chat/Index?chatRoomId=${chatRoomId}`;
            }
        });
    });
}

function scrollToBottom() {
    const messagesContainer = document.getElementById('messagesContainer');
    if (messagesContainer) {
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }
}

function formatTime(timestamp) {
    try {
        // Handle both string and Date objects
        let date;
        if (typeof timestamp === 'string') {
            date = new Date(timestamp);
        } else if (timestamp instanceof Date) {
            date = timestamp;
        } else {
            date = new Date(timestamp);
        }

        if (isNaN(date.getTime())) {
            console.warn('Invalid timestamp:', timestamp);
            return 'Now';
        }

        const now = new Date();
        const diff = now - date;

        if (diff < 60000) { // Less than 1 minute
            return 'Just now';
        } else if (diff < 3600000) { // Less than 1 hour
            const minutes = Math.floor(diff / 60000);
            return `${minutes}m ago`;
        } else if (diff < 86400000) { // Less than 1 day
            const hours = Math.floor(diff / 3600000);
            return `${hours}h ago`;
        } else {
            return date.toLocaleDateString();
        }
    } catch (e) {
        console.error('Date formatting error:', e, 'timestamp:', timestamp);
        return 'Now';
    }
}

function formatFileSize(bytes) {
    if (bytes === 0) return '0 B';

    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function showError(message) {
    // You can implement a toast notification or alert here
    console.error(message);
    alert(message);
}

// Mark messages as read when chat becomes visible
document.addEventListener('visibilitychange', function () {
    if (!document.hidden && currentChatRoomId && currentChatRoomId !== 'new') {
        markMessagesAsRead();
    }
});

// Mark messages as read when scrolling to bottom
document.addEventListener('scroll', function () {
    const messagesContainer = document.getElementById('messagesContainer');
    if (messagesContainer) {
        const isAtBottom = messagesContainer.scrollTop + messagesContainer.clientHeight >= messagesContainer.scrollHeight - 10;
        if (isAtBottom && currentChatRoomId && currentChatRoomId !== 'new') {
            markMessagesAsRead();
        }
    }
});

// Image Modal Functions
function openImageModal(src) {
    document.getElementById('modalImage').src = src;
    document.getElementById('imageModal').style.display = 'block';
}

function closeImageModal() {
    document.getElementById('imageModal').style.display = 'none';
}

// Close modal when clicking outside
document.addEventListener('click', function (e) {
    const modal = document.getElementById('imageModal');
    if (e.target === modal) {
        closeImageModal();
    }
});

// Close modal with Escape key
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        closeImageModal();
    }
});

function initiateVideoCall() {
    // Multiple connection state checks
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        showError('Connection not ready. Please wait a moment and try again.');
        return;
    }

    if (!isConnectionReady) {
        showError('Connection is initializing. Please try again in a moment.');
        return;
    }

    if (!currentChatRoomId) {
        showError('Please start a conversation first before making a video call.');
        return;
    }

    // If we're in a "new" chat, we need to create a chat room first or use targetUserId
    if (currentChatRoomId === 'new') {
        const targetUserId = document.getElementById('targetUserId')?.value;
        if (!targetUserId) {
            showError('Please start a conversation first before making a video call.');
            return;
        }

        const tempChatRoomId = `temp_${Date.now()}`;
        attemptVideoCall(tempChatRoomId, 0);
    } else {
        attemptVideoCall(currentChatRoomId, 0);
    }
}

function attemptVideoCall(chatRoomId, attemptCount) {
    const maxAttempts = 3;

    connection.invoke('StartVideoCall', chatRoomId)
        .then(() => {
            console.log('Video call initiated successfully on attempt', attemptCount + 1);
            // Show the waiting notification
            showCallWaitingNotification();
            // **CRITICAL FIX**: Do NOT store pendingVideoCallUrl here
            // The video call window will be opened when CallAccepted is received
        })
        .catch(err => {
            console.error(`Failed to initiate video call on attempt ${attemptCount + 1}:`, err);

            if (attemptCount < maxAttempts - 1) {
                console.log(`Retrying video call... Attempt ${attemptCount + 2} of ${maxAttempts}`);
                setTimeout(() => {
                    attemptVideoCall(chatRoomId, attemptCount + 1);
                }, 1000 * (attemptCount + 1));
            } else {
                showError('Failed to start video call after multiple attempts. Please try again.');
            }
        });
}

// Call Waiting Notification Functions
function showCallWaitingNotification() {
    const notification = document.getElementById('callWaitingNotification');
    if (notification) {
        notification.style.display = 'block';
    }
}

function hideCallWaitingNotification() {
    const notification = document.getElementById('callWaitingNotification');
    if (notification) {
        notification.style.display = 'none';
    }
}

function cancelVideoCall() {
    console.log('Cancelling video call');

    // Hide the waiting notification
    hideCallWaitingNotification();

    // Clear any pending video call URL
    if (window.pendingVideoCallUrl) {
        window.pendingVideoCallUrl = null;
    }
}