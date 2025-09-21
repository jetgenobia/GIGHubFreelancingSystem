document.addEventListener('DOMContentLoaded', function () {
    const passwordInput = document.getElementById('password-icon');
    const confirmPasswordInput = document.getElementById('confirm-password-icon');
    const passwordRequirements = document.getElementById('passwordRequirements');
    const togglePassword = document.getElementById('togglePassword');
    const toggleConfirm = document.getElementById('toggleConfirmPassword');
    const confirmInput = document.getElementById('confirm-password-icon');
    const eyeOpen = document.getElementById('eyeOpen');
    const eyeClosed = document.getElementById('eyeClosed');
    const eyeOpenC = document.getElementById('eyeOpenConfirm');
    const eyeClosedC = document.getElementById('eyeClosedConfirm');

    // Requirements elements
    const lengthReq = document.getElementById('lengthReq');
    const upperReq = document.getElementById('upperReq');
    const lowerReq = document.getElementById('lowerReq');
    const numberReq = document.getElementById('numberReq');
    const specialReq = document.getElementById('specialReq');
    const strengthBar = document.getElementById('strengthBar');
    const strengthText = document.getElementById('strengthText');
    const passwordMatchMessage = document.getElementById('passwordMatchMessage');

    // Valid checkmark SVG
    const validIcon = '<svg class="validation-icon" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"></path></svg>';

    // Invalid X SVG
    const invalidIcon = '<svg class="validation-icon" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"></path></svg>';

    // Special character regex - no Razor parsing issues here
    const specialCharRegex = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/;

    // Toggle password visibility
    if (togglePassword && passwordInput) {
        togglePassword.addEventListener('click', function () {
            const type = passwordInput.getAttribute('type') === 'password' ? 'text' : 'password';
            passwordInput.setAttribute('type', type);

            if (type === 'text') {
                eyeOpen.classList.add('hidden');
                eyeClosed.classList.remove('hidden');
            } else {
                eyeOpen.classList.remove('hidden');
                eyeClosed.classList.add('hidden');
            }
        });
    }

    if (toggleConfirm && confirmInput) {
        toggleConfirm.addEventListener('click', function () {
            const type = confirmInput.getAttribute('type') === 'password' ? 'text' : 'password';
            confirmInput.setAttribute('type', type);

            if (eyeOpenC && eyeClosedC) {
                if (type === 'text') {
                    eyeOpenC.classList.add('hidden');
                    eyeClosedC.classList.remove('hidden');
                } else {
                    eyeOpenC.classList.remove('hidden');
                    eyeClosedC.classList.add('hidden');
                }
            }
        });
    }

    // Show requirements when password field is focused
    if (passwordInput && passwordRequirements) {
        passwordInput.addEventListener('focus', function () {
            passwordRequirements.classList.remove('hidden');
        });

        // Real-time password validation
        passwordInput.addEventListener('input', function () {
            const password = this.value;
            let score = 0;

            // Length check
            if (password.length >= 8) {
                lengthReq.classList.remove('invalid');
                lengthReq.classList.add('valid');
                lengthReq.querySelector('.validation-icon').outerHTML = validIcon;
                score++;
            } else {
                lengthReq.classList.remove('valid');
                lengthReq.classList.add('invalid');
                lengthReq.querySelector('.validation-icon').outerHTML = invalidIcon;
            }

            // Uppercase letter check
            if (/[A-Z]/.test(password)) {
                upperReq.classList.remove('invalid');
                upperReq.classList.add('valid');
                upperReq.querySelector('.validation-icon').outerHTML = validIcon;
                score++;
            } else {
                upperReq.classList.remove('valid');
                upperReq.classList.add('invalid');
                upperReq.querySelector('.validation-icon').outerHTML = invalidIcon;
            }

            // Lowercase letter check
            if (/[a-z]/.test(password)) {
                lowerReq.classList.remove('invalid');
                lowerReq.classList.add('valid');
                lowerReq.querySelector('.validation-icon').outerHTML = validIcon;
                score++;
            } else {
                lowerReq.classList.remove('valid');
                lowerReq.classList.add('invalid');
                lowerReq.querySelector('.validation-icon').outerHTML = invalidIcon;
            }

            // Number check
            if (/[0-9]/.test(password)) {
                numberReq.classList.remove('invalid');
                numberReq.classList.add('valid');
                numberReq.querySelector('.validation-icon').outerHTML = validIcon;
                score++;
            } else {
                numberReq.classList.remove('valid');
                numberReq.classList.add('invalid');
                numberReq.querySelector('.validation-icon').outerHTML = invalidIcon;
            }

            // Special character check
            if (specialCharRegex.test(password)) {
                specialReq.classList.remove('invalid');
                specialReq.classList.add('valid');
                specialReq.querySelector('.validation-icon').outerHTML = validIcon;
                score++;
            } else {
                specialReq.classList.remove('valid');
                specialReq.classList.add('invalid');
                specialReq.querySelector('.validation-icon').outerHTML = invalidIcon;
            }

            // Update password strength
            updatePasswordStrength(score, password.length);

            // Update input border color
            if (score >= 5 && password.length >= 8) {
                this.classList.remove('border-red-validation');
                this.classList.add('border-green-validation');
            } else if (password.length > 0) {
                this.classList.add('border-red-validation');
                this.classList.remove('border-green-validation');
            } else {
                this.classList.remove('border-red-validation', 'border-green-validation');
            }

            // Check password match if confirm password has value
            if (confirmPasswordInput.value) {
                checkPasswordMatch();
            }
        });
    }

    // Password confirmation validation
    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener('input', checkPasswordMatch);
    }

    function checkPasswordMatch() {
        const password = passwordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        if (confirmPassword.length > 0) {
            if (password === confirmPassword) {
                passwordMatchMessage.textContent = '✓ Passwords match';
                passwordMatchMessage.className = 'mt-2 text-xs text-green-600';
                passwordMatchMessage.classList.remove('hidden');
                confirmPasswordInput.classList.remove('border-red-validation');
                confirmPasswordInput.classList.add('border-green-validation');
            } else {
                passwordMatchMessage.textContent = '✗ Passwords do not match';
                passwordMatchMessage.className = 'mt-2 text-xs text-red-600';
                passwordMatchMessage.classList.remove('hidden');
                confirmPasswordInput.classList.add('border-red-validation');
                confirmPasswordInput.classList.remove('border-green-validation');
            }
        } else {
            passwordMatchMessage.classList.add('hidden');
            confirmPasswordInput.classList.remove('border-red-validation', 'border-green-validation');
        }
    }

    function updatePasswordStrength(score, length) {
        let strength = 'Weak';
        let strengthClass = 'strength-weak';

        if (score >= 5 && length >= 12) {
            strength = 'Strong';
            strengthClass = 'strength-strong';
        } else if (score >= 4 && length >= 8) {
            strength = 'Medium';
            strengthClass = 'strength-medium';
        }

        strengthText.textContent = strength;
        strengthBar.className = `password-strength-bar ${strengthClass}`;
    }
});