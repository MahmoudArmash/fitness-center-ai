// Modern Client-Side Validation with UX Enhancements
(function() {
    'use strict';

    // Initialize validation on page load
    document.addEventListener('DOMContentLoaded', function() {
        initializeValidation();
    });

    function initializeValidation() {
        // Get all forms with validation
        const forms = document.querySelectorAll('form[data-validate]');
        forms.forEach(form => {
            setupFormValidation(form);
        });

        // Also setup forms with validation attributes (using more compatible selector)
        const allForms = document.querySelectorAll('form');
        allForms.forEach(form => {
            if (!form.hasAttribute('data-validation-initialized')) {
                const hasRequiredFields = form.querySelectorAll('input[required], select[required], textarea[required]').length > 0;
                if (hasRequiredFields || form.querySelectorAll('input[type="email"], input[type="number"], input[type="date"]').length > 0) {
                    setupFormValidation(form);
                    form.setAttribute('data-validation-initialized', 'true');
                }
            }
        });
    }

    function setupFormValidation(form) {
        const inputs = form.querySelectorAll('input, select, textarea');
        
        inputs.forEach(input => {
            // Real-time validation on blur
            input.addEventListener('blur', function() {
                validateField(this);
            });

            // Real-time validation on input (for better UX)
            input.addEventListener('input', function() {
                if (this.classList.contains('is-invalid')) {
                    validateField(this);
                }
            });

            // Clear validation on focus
            input.addEventListener('focus', function() {
                this.classList.remove('is-invalid', 'is-valid');
                const feedback = this.parentElement.querySelector('.invalid-feedback, .valid-feedback');
                if (feedback) {
                    feedback.remove();
                }
            });
        });

        // Validate on form submit
        form.addEventListener('submit', function(e) {
            let isValid = true;
            inputs.forEach(input => {
                if (!validateField(input)) {
                    isValid = false;
                }
            });

            if (!isValid) {
                e.preventDefault();
                e.stopPropagation();
                
                // Focus on first invalid field
                const firstInvalid = form.querySelector('.is-invalid');
                if (firstInvalid) {
                    firstInvalid.focus();
                    firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }

            form.classList.add('was-validated');
        });
    }

    function validateField(field) {
        const value = field.value.trim();
        const type = field.type;
        const required = field.hasAttribute('required');
        let isValid = true;
        let errorMessage = '';

        // Remove previous validation classes
        field.classList.remove('is-valid', 'is-invalid');
        const existingFeedback = field.parentElement.querySelector('.invalid-feedback, .valid-feedback');
        if (existingFeedback) {
            existingFeedback.remove();
        }

        // Required field validation
        if (required && !value) {
            isValid = false;
            errorMessage = getRequiredMessage(field);
        }
        // Type-specific validation
        else if (value) {
            switch (type) {
                case 'email':
                    if (!isValidEmail(value)) {
                        isValid = false;
                        errorMessage = 'Please enter a valid email address';
                    }
                    break;
                case 'number':
                    if (!isValidNumber(field)) {
                        isValid = false;
                        errorMessage = getNumberErrorMessage(field);
                    }
                    break;
                case 'date':
                    if (!isValidDate(value, field)) {
                        isValid = false;
                        errorMessage = getDateErrorMessage(field);
                    }
                    break;
                case 'datetime-local':
                    if (!isValidDateTime(value, field)) {
                        isValid = false;
                        errorMessage = 'Please enter a valid date and time';
                    }
                    break;
            }

            // Pattern validation
            if (field.hasAttribute('pattern')) {
                const pattern = new RegExp(field.getAttribute('pattern'));
                if (!pattern.test(value)) {
                    isValid = false;
                    errorMessage = field.getAttribute('data-pattern-message') || 'Please match the required format';
                }
            }

            // Min/Max length validation
            if (field.hasAttribute('minlength')) {
                const minLength = parseInt(field.getAttribute('minlength'));
                if (value.length < minLength) {
                    isValid = false;
                    errorMessage = `Please enter at least ${minLength} characters`;
                }
            }

            if (field.hasAttribute('maxlength')) {
                const maxLength = parseInt(field.getAttribute('maxlength'));
                if (value.length > maxLength) {
                    isValid = false;
                    errorMessage = `Please enter no more than ${maxLength} characters`;
                }
            }
        }

        // Apply validation classes and feedback
        if (isValid && value) {
            field.classList.add('is-valid');
            showValidFeedback(field);
        } else if (!isValid) {
            field.classList.add('is-invalid');
            showInvalidFeedback(field, errorMessage);
        }

        return isValid;
    }

    function isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }

    function isValidNumber(field) {
        const value = parseFloat(field.value);
        if (isNaN(value)) return false;

        if (field.hasAttribute('min')) {
            const min = parseFloat(field.getAttribute('min'));
            if (value < min) return false;
        }

        if (field.hasAttribute('max')) {
            const max = parseFloat(field.getAttribute('max'));
            if (value > max) return false;
        }

        return true;
    }

    function getNumberErrorMessage(field) {
        const value = parseFloat(field.value);
        if (field.hasAttribute('min') && value < parseFloat(field.getAttribute('min'))) {
            return `Value must be at least ${field.getAttribute('min')}`;
        }
        if (field.hasAttribute('max') && value > parseFloat(field.getAttribute('max'))) {
            return `Value must be at most ${field.getAttribute('max')}`;
        }
        return 'Please enter a valid number';
    }

    function isValidDate(dateString, field) {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return false;

        if (field.hasAttribute('min')) {
            const minDate = new Date(field.getAttribute('min'));
            if (date < minDate) return false;
        }

        if (field.hasAttribute('max')) {
            const maxDate = new Date(field.getAttribute('max'));
            if (date > maxDate) return false;
        }

        return true;
    }

    function getDateErrorMessage(field) {
        if (field.hasAttribute('min')) {
            return `Date must be after ${new Date(field.getAttribute('min')).toLocaleDateString()}`;
        }
        if (field.hasAttribute('max')) {
            return `Date must be before ${new Date(field.getAttribute('max')).toLocaleDateString()}`;
        }
        return 'Please enter a valid date';
    }

    function isValidDateTime(dateTimeString, field) {
        const date = new Date(dateTimeString);
        if (isNaN(date.getTime())) return false;

        // Check if date is in the future for appointments
        if (field.id === 'appointmentDateTime' || field.name === 'AppointmentDateTime' || field.name === 'appointmentDateTime') {
            const now = new Date();
            now.setMinutes(now.getMinutes() - 1); // Allow 1 minute buffer
            if (date < now) {
                return false;
            }
        }

        return true;
    }

    function getRequiredMessage(field) {
        const label = field.parentElement.querySelector('label');
        const fieldName = label ? label.textContent.replace('*', '').trim() : 'This field';
        return `${fieldName} is required`;
    }

    function showInvalidFeedback(field, message) {
        const feedback = document.createElement('div');
        feedback.className = 'invalid-feedback';
        feedback.textContent = message;
        
        // Insert after the field or its parent (for floating labels)
        const parent = field.parentElement;
        if (parent.classList.contains('form-floating')) {
            parent.appendChild(feedback);
        } else {
            field.parentElement.insertBefore(feedback, field.nextSibling);
        }
    }

    function showValidFeedback(field) {
        // Only show valid feedback for certain fields
        if (field.type === 'email' || field.type === 'password') {
            const feedback = document.createElement('div');
            feedback.className = 'valid-feedback';
            feedback.textContent = 'Looks good!';
            
            const parent = field.parentElement;
            if (parent.classList.contains('form-floating')) {
                parent.appendChild(feedback);
            } else {
                field.parentElement.insertBefore(feedback, field.nextSibling);
            }
        }
    }

    // Custom validation for appointment form
    function validateAppointmentForm() {
        const appointmentForm = document.getElementById('appointmentForm');
        if (!appointmentForm) return;

        const serviceSelect = document.getElementById('serviceSelect');
        const dateTimeInput = document.getElementById('appointmentDateTime');
        const trainerSelect = document.getElementById('trainerSelect');

        // Validate service selection
        if (serviceSelect) {
            serviceSelect.addEventListener('change', function() {
                if (this.value) {
                    this.classList.remove('is-invalid');
                    this.classList.add('is-valid');
                }
            });
        }

        // Validate date/time
        if (dateTimeInput) {
            dateTimeInput.addEventListener('change', function() {
                const selectedDate = new Date(this.value);
                const now = new Date();
                now.setMinutes(now.getMinutes() - 1); // Allow 1 minute buffer
                
                if (selectedDate < now) {
                    this.classList.add('is-invalid');
                    this.classList.remove('is-valid');
                    showInvalidFeedback(this, 'Appointment date must be in the future');
                } else if (this.value) {
                    this.classList.remove('is-invalid');
                    this.classList.add('is-valid');
                }
            });
        }

        // Validate trainer selection
        if (trainerSelect) {
            trainerSelect.addEventListener('change', function() {
                if (this.value && this.value !== '') {
                    this.classList.remove('is-invalid');
                    this.classList.add('is-valid');
                } else if (this.value === '' && serviceSelect && serviceSelect.value && dateTimeInput && dateTimeInput.value) {
                    this.classList.add('is-invalid');
                    showInvalidFeedback(this, 'Please select a trainer');
                }
            });
        }
    }

    // Initialize appointment form validation
    if (document.getElementById('appointmentForm')) {
        validateAppointmentForm();
    }

    // Export for global use
    window.FormValidation = {
        validateField: validateField,
        validateForm: function(form) {
            const inputs = form.querySelectorAll('input, select, textarea');
            let isValid = true;
            inputs.forEach(input => {
                if (!validateField(input)) {
                    isValid = false;
                }
            });
            return isValid;
        }
    };
})();
