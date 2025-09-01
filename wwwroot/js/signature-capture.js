// Digital Signature Capture with HTML5 Canvas
// Supports both mouse and touch input for mobile devices

class SignatureCapture {
    constructor(canvasId, options = {}) {
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) {
            throw new Error(`Canvas element with id '${canvasId}' not found`);
        }

        this.ctx = this.canvas.getContext('2d');
        this.isDrawing = false;
        this.hasSignature = false;
        
        // Configuration options
        this.options = {
            penColor: options.penColor || '#000000',
            penWidth: options.penWidth || 2,
            backgroundColor: options.backgroundColor || '#ffffff',
            smoothing: options.smoothing !== false, // default true
            ...options
        };

        // Signature data storage
        this.signatureData = [];
        this.currentStroke = [];

        this.initializeCanvas();
        this.attachEventListeners();
    }

    initializeCanvas() {
        // Set canvas size
        const rect = this.canvas.getBoundingClientRect();
        this.canvas.width = rect.width * window.devicePixelRatio;
        this.canvas.height = rect.height * window.devicePixelRatio;
        
        // Scale context for high DPI displays
        this.ctx.scale(window.devicePixelRatio, window.devicePixelRatio);
        
        // Set canvas style
        this.canvas.style.width = rect.width + 'px';
        this.canvas.style.height = rect.height + 'px';

        // Configure drawing context
        this.ctx.strokeStyle = this.options.penColor;
        this.ctx.lineWidth = this.options.penWidth;
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
        
        if (this.options.smoothing) {
            this.ctx.globalCompositeOperation = 'source-over';
        }

        this.clearCanvas();
    }

    attachEventListeners() {
        // Mouse events
        this.canvas.addEventListener('mousedown', this.startDrawing.bind(this));
        this.canvas.addEventListener('mousemove', this.draw.bind(this));
        this.canvas.addEventListener('mouseup', this.stopDrawing.bind(this));
        this.canvas.addEventListener('mouseout', this.stopDrawing.bind(this));

        // Touch events for mobile
        this.canvas.addEventListener('touchstart', this.handleTouch.bind(this));
        this.canvas.addEventListener('touchmove', this.handleTouch.bind(this));
        this.canvas.addEventListener('touchend', this.stopDrawing.bind(this));
        this.canvas.addEventListener('touchcancel', this.stopDrawing.bind(this));

        // Prevent scrolling when touching the canvas
        this.canvas.addEventListener('touchstart', (e) => e.preventDefault());
        this.canvas.addEventListener('touchmove', (e) => e.preventDefault());
    }

    getCoordinates(event) {
        const rect = this.canvas.getBoundingClientRect();
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;

        if (event.touches) {
            // Touch event
            const touch = event.touches[0];
            return {
                x: (touch.clientX - rect.left) * scaleX / window.devicePixelRatio,
                y: (touch.clientY - rect.top) * scaleY / window.devicePixelRatio
            };
        } else {
            // Mouse event
            return {
                x: (event.clientX - rect.left) * scaleX / window.devicePixelRatio,
                y: (event.clientY - rect.top) * scaleY / window.devicePixelRatio
            };
        }
    }

    startDrawing(event) {
        this.isDrawing = true;
        this.hasSignature = true;
        
        const coords = this.getCoordinates(event);
        this.currentStroke = [coords];
        
        this.ctx.beginPath();
        this.ctx.moveTo(coords.x, coords.y);
        
        // Trigger signature start event
        this.dispatchEvent('signatureStart', { coordinates: coords });
    }

    draw(event) {
        if (!this.isDrawing) return;

        const coords = this.getCoordinates(event);
        this.currentStroke.push(coords);

        if (this.options.smoothing && this.currentStroke.length > 2) {
            // Use quadratic curves for smoother lines
            const prevPoint = this.currentStroke[this.currentStroke.length - 2];
            const currentPoint = coords;
            const controlPoint = {
                x: (prevPoint.x + currentPoint.x) / 2,
                y: (prevPoint.y + currentPoint.y) / 2
            };

            this.ctx.quadraticCurveTo(prevPoint.x, prevPoint.y, controlPoint.x, controlPoint.y);
        } else {
            this.ctx.lineTo(coords.x, coords.y);
        }

        this.ctx.stroke();
        
        // Trigger signature draw event
        this.dispatchEvent('signatureDraw', { coordinates: coords });
    }

    stopDrawing() {
        if (!this.isDrawing) return;
        
        this.isDrawing = false;
        
        if (this.currentStroke.length > 0) {
            this.signatureData.push([...this.currentStroke]);
            this.currentStroke = [];
        }
        
        // Trigger signature end event
        this.dispatchEvent('signatureEnd', { 
            hasSignature: this.hasSignature,
            strokeCount: this.signatureData.length 
        });
    }

    handleTouch(event) {
        event.preventDefault();
        
        const touch = event.touches[0];
        const mouseEvent = new MouseEvent(event.type.replace('touch', 'mouse'), {
            clientX: touch.clientX,
            clientY: touch.clientY
        });

        if (event.type === 'touchstart') {
            this.startDrawing(event);
        } else if (event.type === 'touchmove') {
            this.draw(event);
        }
    }

    clearCanvas() {
        this.ctx.fillStyle = this.options.backgroundColor;
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
        
        this.hasSignature = false;
        this.signatureData = [];
        this.currentStroke = [];
        
        // Trigger clear event
        this.dispatchEvent('signatureClear');
    }

    getSignatureDataURL(format = 'image/png', quality = 0.8) {
        if (!this.hasSignature) {
            return null;
        }
        return this.canvas.toDataURL(format, quality);
    }

    getSignatureBlob(callback, format = 'image/png', quality = 0.8) {
        if (!this.hasSignature) {
            callback(null);
            return;
        }
        this.canvas.toBlob(callback, format, quality);
    }

    getSignatureBase64(includeDataUrl = false) {
        const dataUrl = this.getSignatureDataURL();
        if (!dataUrl) return null;
        
        if (includeDataUrl) {
            return dataUrl;
        } else {
            return dataUrl.split(',')[1]; // Remove data:image/png;base64, prefix
        }
    }

    isEmpty() {
        return !this.hasSignature;
    }

    setSignatureFromDataURL(dataUrl) {
        const img = new Image();
        img.onload = () => {
            this.clearCanvas();
            this.ctx.drawImage(img, 0, 0);
            this.hasSignature = true;
            this.dispatchEvent('signatureLoaded', { dataUrl });
        };
        img.src = dataUrl;
    }

    resize() {
        // Store current signature if any
        const currentSignature = this.hasSignature ? this.getSignatureDataURL() : null;
        
        // Reinitialize canvas
        this.initializeCanvas();
        
        // Restore signature if it existed
        if (currentSignature) {
            this.setSignatureFromDataURL(currentSignature);
        }
    }

    dispatchEvent(eventName, detail = {}) {
        const event = new CustomEvent(eventName, {
            detail: {
                signatureCapture: this,
                ...detail
            }
        });
        this.canvas.dispatchEvent(event);
    }

    // Validation methods
    validate() {
        const errors = [];
        
        if (!this.hasSignature) {
            errors.push('Signature is required');
        }
        
        return {
            isValid: errors.length === 0,
            errors: errors
        };
    }

    // Export signature data for storage/transmission
    exportData() {
        return {
            signatureData: this.signatureData,
            canvasSize: {
                width: this.canvas.width,
                height: this.canvas.height
            },
            options: this.options,
            timestamp: new Date().toISOString(),
            hasSignature: this.hasSignature
        };
    }

    // Import signature data
    importData(data) {
        this.clearCanvas();
        
        if (data.signatureData && data.signatureData.length > 0) {
            this.ctx.beginPath();
            
            data.signatureData.forEach(stroke => {
                if (stroke.length > 0) {
                    this.ctx.moveTo(stroke[0].x, stroke[0].y);
                    stroke.forEach((point, index) => {
                        if (index > 0) {
                            this.ctx.lineTo(point.x, point.y);
                        }
                    });
                }
            });
            
            this.ctx.stroke();
            this.signatureData = data.signatureData;
            this.hasSignature = true;
        }
    }
}

// Utility functions for signature management
const SignatureUtils = {
    // Initialize signature capture with common options
    createSignatureCapture: function(canvasId, options = {}) {
        const defaultOptions = {
            penColor: '#2563eb',
            penWidth: 2,
            backgroundColor: '#ffffff',
            smoothing: true
        };
        
        return new SignatureCapture(canvasId, { ...defaultOptions, ...options });
    },

    // Setup signature form integration
    setupSignatureForm: function(signatureCapture, formId, signatureFieldId) {
        const form = document.getElementById(formId);
        const signatureField = document.getElementById(signatureFieldId);
        
        if (!form || !signatureField) {
            console.error('Form or signature field not found');
            return;
        }

        form.addEventListener('submit', function(e) {
            if (signatureCapture.isEmpty()) {
                e.preventDefault();
                alert('Please provide your signature before submitting.');
                return false;
            }

            const validation = signatureCapture.validate();
            if (!validation.isValid) {
                e.preventDefault();
                alert('Signature validation failed: ' + validation.errors.join(', '));
                return false;
            }

            // Set signature data in hidden field
            signatureField.value = signatureCapture.getSignatureBase64();
        });
    },

    // Create signature preview
    createSignaturePreview: function(signatureData, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const canvas = document.createElement('canvas');
        canvas.width = 200;
        canvas.height = 80;
        canvas.style.border = '1px solid #ccc';
        canvas.style.borderRadius = '4px';

        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        if (signatureData) {
            const img = new Image();
            img.onload = function() {
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            };
            img.src = signatureData;
        }

        container.innerHTML = '';
        container.appendChild(canvas);
    }
};

// Make classes available globally
window.SignatureCapture = SignatureCapture;
window.SignatureUtils = SignatureUtils;

