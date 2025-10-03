// Global Video Call Notification System for both Mentorship and Project Chat
class GlobalVideoCall {
    constructor() {
        this.mentorshipConnection = null;
        this.projectConnection = null;
        this.currentUserId = null;
        this.isMentorshipConnected = false;
        this.isProjectConnected = false;
        this.notificationContainer = null;
        this.init();
    }

    async init() {
        try {
            console.log('GlobalVideoCall: Initializing...');
            // Get current user ID from page
            this.currentUserId = this.getCurrentUserId();
            console.log('GlobalVideoCall: Current user ID:', this.currentUserId);
            if (!this.currentUserId) {
                console.log('GlobalVideoCall: No user ID found, skipping initialization');
                return;
            }

            // Create notification container
            this.createNotificationContainer();
            
            // Connect to both SignalR hubs
            await this.connectToMentorshipSignalR();
            await this.connectToProjectSignalR();
            
            // Set up event handlers
            this.setupEventHandlers();
            
            console.log('GlobalVideoCall: Initialization complete');
        } catch (error) {
            console.error('GlobalVideoCall: Error initializing:', error);
        }
    }

    getCurrentUserId() {
        // Try to get user ID from body data attribute
        const body = document.querySelector('body[data-user-id]');
        if (body && body.dataset.userId) {
            return body.dataset.userId;
        }

        // Try to get user ID from various sources
        const userIdElement = document.querySelector('[data-user-id]');
        if (userIdElement && userIdElement.dataset.userId) {
            return userIdElement.dataset.userId;
        }

        // Check if we're on a chat page
        const matchIdElement = document.querySelector('[data-match-id]');
        if (matchIdElement) {
            // Extract from URL or other sources
            const urlParams = new URLSearchParams(window.location.search);
            const userId = urlParams.get('userId') || this.extractUserIdFromPage();
            if (userId) {
                return userId;
            }
        }

        return null;
    }

    extractUserIdFromPage() {
        // Try to extract from various page elements
        const scripts = document.querySelectorAll('script');
        for (const script of scripts) {
            const text = script.textContent;
            const match = text.match(/currentUserId['"]?\s*[:=]\s*['"]([^'"]+)['"]/);
            if (match) {
                return match[1];
            }
        }
        return null;
    }

    createNotificationContainer() {
        // Remove existing container if any
        const existing = document.getElementById('global-video-notification');
        if (existing) {
            existing.remove();
        }

        // Create new container
        this.notificationContainer = document.createElement('div');
        this.notificationContainer.id = 'global-video-notification';
        this.notificationContainer.style.cssText = `
            position: fixed !important;
            top: 20px !important;
            right: 20px !important;
            z-index: 99999 !important;
            max-width: 400px !important;
            min-width: 300px !important;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif !important;
            pointer-events: auto !important;
            display: block !important;
            visibility: visible !important;
        `;

        document.body.appendChild(this.notificationContainer);
    }

    async connectToMentorshipSignalR() {
        try {
            console.log('GlobalVideoCall: Connecting to Mentorship SignalR...');
            this.mentorshipConnection = new signalR.HubConnectionBuilder()
                .withUrl('/mentorshipChatHub')
                .withAutomaticReconnect()
                .build();

            await this.mentorshipConnection.start();
            this.isMentorshipConnected = true;
            console.log('GlobalVideoCall: Mentorship SignalR connected successfully');

            // Join global room for this user
            await this.mentorshipConnection.invoke('JoinUserRoom', this.currentUserId);
            console.log('GlobalVideoCall: Joined mentorship user room:', this.currentUserId);

        } catch (error) {
            console.error('GlobalVideoCall: Failed to connect to Mentorship SignalR:', error);
        }
    }

    async connectToProjectSignalR() {
        try {
            console.log('GlobalVideoCall: Connecting to Project SignalR...');

            if (this.projectConnection) {
                await this.projectConnection.stop();
            }

            this.projectConnection = new signalR.HubConnectionBuilder()
                .withUrl('/chatHub')
                .withAutomaticReconnect([0, 2000, 10000, 30000])
                .build();

            // Better connection handling
            this.projectConnection.onclose(() => {
                this.isProjectConnected = false;
                console.log('GlobalVideoCall: Project SignalR connection closed');
            });

            this.projectConnection.onreconnecting(() => {
                this.isProjectConnected = false;
                console.log('GlobalVideoCall: Project SignalR reconnecting...');
            });

            this.projectConnection.onreconnected(() => {
                this.isProjectConnected = true;
                console.log('GlobalVideoCall: Project SignalR reconnected');
                // Rejoin user room
                if (this.currentUserId) {
                    this.projectConnection.invoke('JoinUserRoom', this.currentUserId);
                }
            });

            await this.projectConnection.start();
            this.isProjectConnected = true;
            console.log('GlobalVideoCall: Project SignalR connected successfully');

            // Join user room immediately
            if (this.currentUserId) {
                await this.projectConnection.invoke('JoinUserRoom', this.currentUserId);
                console.log('GlobalVideoCall: Joined project user room:', this.currentUserId);
            }

        } catch (error) {
            console.error('GlobalVideoCall: Failed to connect to Project SignalR:', error);
            this.isProjectConnected = false;
        }
    }

    setupEventHandlers() {
        // Setup mentorship event handlers
        if (this.mentorshipConnection) {
            this.setupMentorshipEventHandlers();
        }

        // Setup project event handlers
        if (this.projectConnection) {
            this.setupProjectEventHandlers();
        }
    }

    setupMentorshipEventHandlers() {
        // Handle incoming mentorship video calls
        this.mentorshipConnection.on('IncomingVideoCall', (data) => {
            console.log('GlobalVideoCall: Received mentorship incoming call:', data);
            this.showIncomingCallNotification(data, 'mentorship');
        });

        // Handle mentorship call waiting events
        this.mentorshipConnection.on('CallRequested', (data) => {
            this.showCallWaitingNotification(data, 'mentorship');
        });

        this.mentorshipConnection.on('CallAccepted', (data) => {
            this.hideCallWaitingNotification();
        });

        this.mentorshipConnection.on('CallDeclined', (data) => {
            this.hideCallWaitingNotification();
        });

        // Handle mentorship video call ended
        this.mentorshipConnection.on('VideoCallEnded', () => {
            this.hideNotification();
        });

        // Handle mentorship connection state changes
        this.mentorshipConnection.onclose(() => {
            this.isMentorshipConnected = false;
        });

        this.mentorshipConnection.onreconnecting(() => {
            // Reconnecting...
        });

        this.mentorshipConnection.onreconnected(() => {
            this.isMentorshipConnected = true;
            // Rejoin user room
            this.mentorshipConnection.invoke('JoinUserRoom', this.currentUserId);
        });

        // Handle mentorship errors
        this.mentorshipConnection.on('Error', (error) => {
            console.error('GlobalVideoCall: Mentorship SignalR error:', error);
        });
    }

    setupProjectEventHandlers() {
        // Handle incoming project video calls
        this.projectConnection.on('IncomingVideoCall', (data) => {
            console.log('GlobalVideoCall: Received project incoming call:', data);
            this.showIncomingCallNotification(data, 'project');
        });

        // Handle project call waiting events
        this.projectConnection.on('CallRequested', (data) => {
            console.log('GlobalVideoCall: Call requested (waiting for response):', data);
            this.showCallWaitingNotification(data, 'project');
            // **CRITICAL FIX**: Do NOT open video call window here - wait for acceptance
        });

        this.projectConnection.on('CallAccepted', (data) => {
            console.log('GlobalVideoCall: Call accepted with data:', data);
            this.hideCallWaitingNotification();

            // **CRITICAL FIX**: Open video call window only after acceptance with proper ChatRoomId
            if (data.ChatRoomId && data.ChatRoomId !== 'undefined') {
                const videoCallUrl = `/Chat/VideoCall?chatRoomId=${data.ChatRoomId}`;
                console.log('GlobalVideoCall: Opening video call window after acceptance:', videoCallUrl);
                window.open(videoCallUrl, 'VideoCall', 'width=800,height=600,scrollbars=no,resizable=yes');
            } else if (data.IsTemporary && document.getElementById('targetUserId')) {
                const targetUserId = document.getElementById('targetUserId').value;
                const videoCallUrl = `/Chat/VideoCall?targetUserId=${targetUserId}`;
                console.log('GlobalVideoCall: Opening video call window for temporary chat:', videoCallUrl);
                window.open(videoCallUrl, 'VideoCall', 'width=800,height=600,scrollbars=no,resizable=yes');
            } else {
                console.error('GlobalVideoCall: No valid ChatRoomId in CallAccepted data:', data);
            }
        });

        this.projectConnection.on('CallDeclined', (data) => {
            console.log('GlobalVideoCall: Call declined:', data);
            this.hideCallWaitingNotification();
        });

        // Handle project video call ended
        this.projectConnection.on('VideoCallEnded', () => {
            this.hideNotification();
        });

        // Handle project connection state changes
        this.projectConnection.onclose(() => {
            this.isProjectConnected = false;
        });

        this.projectConnection.onreconnecting(() => {
            // Reconnecting...
        });

        this.projectConnection.onreconnected(() => {
            this.isProjectConnected = true;
            // Rejoin user room
            this.projectConnection.invoke('JoinUserRoom', this.currentUserId);
        });

        // Handle project errors
        this.projectConnection.on('Error', (error) => {
            console.error('GlobalVideoCall: Project SignalR error:', error);
        });
    }

    showIncomingCallNotification(data, type = 'mentorship') {
        console.log('GlobalVideoCall: Showing notification with data:', data, 'type:', type);
        console.log('GlobalVideoCall: Data keys:', Object.keys(data));
        console.log('GlobalVideoCall: Data values:', Object.values(data));
        const notification = document.createElement('div');
        notification.className = 'video-call-notification';
        
        // Create a beautiful notification
        notification.style.cssText = `
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
            color: white !important;
            padding: 20px !important;
            border-radius: 12px !important;
            box-shadow: 0 10px 25px rgba(0,0,0,0.2) !important;
            margin-bottom: 10px !important;
            animation: slideIn 0.3s ease-out !important;
            backdrop-filter: blur(10px) !important;
            border: 1px solid rgba(255,255,255,0.1) !important;
            position: relative !important;
            z-index: 10000 !important;
            min-width: 300px !important;
            max-width: 400px !important;
            display: block !important;
        `;

        // Force display block directly on the element
        notification.style.display = 'block';
        notification.style.visibility = 'visible';
        notification.style.opacity = '1';

        // Determine the caller name and ID based on type - handle both camelCase and PascalCase
        let callerName, callerId, callId, callerPhoto;
        
        if (type === 'mentorship') {
            callerName = data.callerName || data.CallerName || 'Unknown';
            callerId = data.callerId || data.CallerId;
            callId = data.mentorshipMatchId || data.MentorshipMatchId;
            callerPhoto = data.callerPhoto || data.CallerPhoto;
        } else {
            callerName = data.CallerName || data.callerName || 'Unknown';
            callerId = data.CallerId || data.callerId;
            callId = data.ChatRoomId || data.chatRoomId;
            callerPhoto = data.CallerPhoto || data.callerPhoto;
        }
        
        console.log('GlobalVideoCall: Extracted data - callerName:', callerName, 'callerId:', callerId, 'callId:', callId);

        // Validate that we have the required data
        if (!callerId || !callId) {
            console.error('GlobalVideoCall: Missing required data for notification - callerId:', callerId, 'callId:', callId);
            return;
        }

        // Create the photo element - show user photo if available, otherwise show default GIGHub profile image
        const photoElement = callerPhoto && callerPhoto.trim() !== '' 
            ? `<img src="${callerPhoto}" alt="${callerName}" style="width: 50px; height: 50px; border-radius: 50%; object-fit: cover; border: 2px solid rgba(255,255,255,0.3);">`
            : `<img src="https://ik.imagekit.io/6txj3mofs/GIGHub%20(11).png?updatedAt=1750552804497" alt="${callerName}" style="width: 50px; height: 50px; border-radius: 50%; object-fit: cover; border: 2px solid rgba(255,255,255,0.3);">`;

        notification.innerHTML = `
            <div style="display: flex; align-items: center; gap: 15px;">
                <div style="flex-shrink: 0;">
                    ${photoElement}
                </div>
                <div style="flex-grow: 1;">
                    <h4 style="margin: 0 0 5px 0; font-size: 16px; font-weight: 600;">
                        Incoming Video Call
                    </h4>
                    <p style="margin: 0; font-size: 14px; opacity: 0.9;">
                        ${callerName} is calling...
                    </p>
                </div>
                <div style="flex-shrink: 0; display: flex; gap: 8px;">
                    <button onclick="window.globalVideoCall.acceptCall('${callId}', '${callerId}', '${type}')" 
                            style="background: #10b981; border: none; color: white; padding: 8px 12px; border-radius: 6px; cursor: pointer; font-size: 12px;">
                        <i class="fas fa-phone"></i> Accept
                    </button>
                    <button onclick="window.globalVideoCall.declineCall('${callId}', '${callerId}', '${type}')" 
                            style="background: #ef4444; border: none; color: white; padding: 8px 12px; border-radius: 6px; cursor: pointer; font-size: 12px;">
                        <i class="fas fa-phone-slash"></i> Decline
                    </button>
                </div>
            </div>
        `;

        // Add CSS animation
        const style = document.createElement('style');
        style.textContent = `
            @keyframes slideIn {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
            @keyframes slideOut {
                from { transform: translateX(0); opacity: 1; }
                to { transform: translateX(100%); opacity: 0; }
            }
        `;
        document.head.appendChild(style);

        if (this.notificationContainer) {
            this.notificationContainer.appendChild(notification);
        } else {
            console.error('GlobalVideoCall: Notification container is null!');
        }

        // Auto-hide after 30 seconds
        setTimeout(() => {
            this.hideNotification();
        }, 30000);
    }

    hideNotification() {
        if (this.notificationContainer) {
            const notifications = this.notificationContainer.querySelectorAll('.video-call-notification');
            notifications.forEach(notification => {
                notification.style.animation = 'slideOut 0.3s ease-in';
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.remove();
                    }
                }, 300);
            });
        }
    }

    showCallWaitingNotification(data) {
        const notification = document.createElement('div');
        notification.className = 'call-waiting-notification';
        
        // Create a beautiful waiting notification
        notification.style.cssText = `
            background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%) !important;
            color: white !important;
            padding: 20px !important;
            border-radius: 12px !important;
            box-shadow: 0 10px 25px rgba(0,0,0,0.2) !important;
            margin-bottom: 10px !important;
            animation: slideIn 0.3s ease-out !important;
            backdrop-filter: blur(10px) !important;
            border: 1px solid rgba(255,255,255,0.1) !important;
            position: relative !important;
            z-index: 10000 !important;
            min-width: 300px !important;
            max-width: 400px !important;
            display: block !important;
        `;

        // Force display block directly on the element
        notification.style.display = 'block';
        notification.style.visibility = 'visible';
        notification.style.opacity = '1';

        notification.innerHTML = `
            <div style="display: flex; align-items: center; gap: 15px;">
                <div style="flex-shrink: 0;">
                    <div style="width: 50px; height: 50px; background: rgba(255,255,255,0.2); border-radius: 50%; display: flex; align-items: center; justify-content: center;">
                        <i class="fas fa-phone" style="font-size: 20px;"></i>
                    </div>
                </div>
                <div style="flex-grow: 1;">
                    <h4 style="margin: 0 0 5px 0; font-size: 16px; font-weight: 600;">
                        Calling...
                    </h4>
                    <p style="margin: 0; font-size: 14px; opacity: 0.9;">
                        Waiting for response
                    </p>
                </div>
                <div style="flex-shrink: 0;">
                    <button onclick="window.globalVideoCall.cancelCall()" 
                            style="background: #ef4444; border: none; color: white; padding: 8px 12px; border-radius: 6px; cursor: pointer; font-size: 12px;">
                        <i class="fas fa-times"></i> Cancel
                    </button>
                </div>
            </div>
        `;

        if (this.notificationContainer) {
            this.notificationContainer.appendChild(notification);
        } else {
            console.error('GlobalVideoCall: Notification container is null!');
        }
    }

    hideCallWaitingNotification() {
        if (this.notificationContainer) {
            const notifications = this.notificationContainer.querySelectorAll('.call-waiting-notification');
            notifications.forEach(notification => {
                notification.style.animation = 'slideOut 0.3s ease-in';
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.remove();
                    }
                }, 300);
            });
        }
    }

    cancelCall() {
        // Hide the waiting notification
        this.hideCallWaitingNotification();
        
        // Note: We don't need to send a specific cancel event to the server
        // The waiting notification will just disappear, and if the other person
        // tries to accept/decline later, it won't affect anything
    }

    async acceptCall(matchId, callerId, type = 'mentorship') {
        try {
            console.log('GlobalVideoCall: Accepting call:', matchId, callerId, type);

            // Check connection state before accepting
            if (type === 'project' && (!this.projectConnection || !this.isProjectConnected)) {
                console.error('Project connection not ready');
                alert('Connection not ready. Please refresh the page and try again.');
                return;
            }

            if (type === 'mentorship' && (!this.mentorshipConnection || !this.isMentorshipConnected)) {
                console.error('Mentorship connection not ready');
                alert('Connection not ready. Please refresh the page and try again.');
                return;
            }

            // Hide notification
            this.hideNotification();

            // Send accept signal based on type with retry logic
            if (type === 'mentorship' && this.mentorshipConnection && this.isMentorshipConnected) {
                await this.retryInvoke(this.mentorshipConnection, 'AcceptVideoCall', matchId, callerId);
                // Open mentorship video call window
                const videoCallUrl = `/MentorshipChat/VideoCall/${matchId}`;
                window.open(videoCallUrl, 'videoCall', 'width=1200,height=800,scrollbars=no,resizable=yes');
            } else if (type === 'project' && this.projectConnection && this.isProjectConnected) {
                await this.retryInvoke(this.projectConnection, 'AcceptVideoCall', matchId, callerId);
                // Open project video call window
                const videoCallUrl = `/Chat/VideoCall?chatRoomId=${matchId}`;
                window.open(videoCallUrl, 'videoCall', 'width=1200,height=800,scrollbars=no,resizable=yes');
            }

        } catch (error) {
            console.error('GlobalVideoCall: Error accepting call:', error);
            alert('Failed to accept video call. Please try again.');
        }
    }

    async retryInvoke(connection, methodName, ...args) {
        const maxAttempts = 3;

        for (let attempt = 0; attempt < maxAttempts; attempt++) {
            try {
                await connection.invoke(methodName, ...args);
                console.log(`${methodName} succeeded on attempt ${attempt + 1}`);
                return;
            } catch (error) {
                console.error(`${methodName} failed on attempt ${attempt + 1}:`, error);

                if (attempt < maxAttempts - 1) {
                    // Wait before retrying
                    await new Promise(resolve => setTimeout(resolve, 500 * (attempt + 1)));
                } else {
                    throw error;
                }
            }
        }
    }

    async declineCall(matchId, callerId, type = 'mentorship') {
        try {
            console.log('GlobalVideoCall: Declining call:', matchId, callerId, type);
            // Hide notification
            this.hideNotification();
            
            // Send decline signal based on type
            if (type === 'mentorship' && this.mentorshipConnection && this.isMentorshipConnected) {
                await this.mentorshipConnection.invoke('DeclineVideoCall', matchId, callerId);
            } else if (type === 'project' && this.projectConnection && this.isProjectConnected) {
                await this.projectConnection.invoke('DeclineVideoCall', matchId, callerId);
            }
            
        } catch (error) {
            console.error('GlobalVideoCall: Error declining call:', error);
        }
    }

    // Public method to check if connected
    isSignalRConnected() {
        return this.isMentorshipConnected || this.isProjectConnected;
    }
}

// Initialize global video call system when page loads
let globalVideoCall = null;

document.addEventListener('DOMContentLoaded', () => {
    // Only initialize if SignalR is available
    if (typeof signalR !== 'undefined') {
        globalVideoCall = new GlobalVideoCall();
        
        // Make it globally accessible
        window.globalVideoCall = globalVideoCall;
    }
});

// Also make it globally accessible immediately
window.globalVideoCall = globalVideoCall; 