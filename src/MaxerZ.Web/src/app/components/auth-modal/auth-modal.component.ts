import { Component, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './auth-modal.component.html',
  styleUrl: './auth-modal.component.scss'
})
export class AuthModalComponent {
  closeModal = output<void>();

  mode = signal<'login' | 'register'>('login');
  email = '';
  password = '';
  fullName = '';
  errorMessage = signal<string | null>(null);
  isLoading = signal(false);

  constructor(private authService: AuthService) {}

  switchMode(newMode: 'login' | 'register') {
    this.mode.set(newMode);
    this.errorMessage.set(null);
  }

  submit() {
    this.errorMessage.set(null);

    const cleanEmail = (this.email || '').trim();
    const cleanPassword = (this.password || '').trim();
    const cleanFullName = (this.fullName || '').trim();

    if (!cleanEmail || !cleanPassword) {
      this.errorMessage.set('Please enter your email and password.');
      return;
    }

    this.isLoading.set(true);

    if (this.mode() === 'login') {
      this.authService.login({ email: cleanEmail, password: cleanPassword }).subscribe({
        next: (res) => {
          this.isLoading.set(false);
          if (res.success) {
            this.closeModal.emit();
          } else {
            this.errorMessage.set(res.message || 'Login failed.');
          }
        },
        error: (err) => {
          this.isLoading.set(false);
          const msg = err.error?.message || err.error?.error || 'Invalid email or password.';
          this.errorMessage.set(msg);
        }
      });
    } else {
      this.authService.register({
        email: cleanEmail,
        password: cleanPassword,
        fullName: cleanFullName
      }).subscribe({
        next: (res) => {
          this.isLoading.set(false);
          if (res.success) {
            this.closeModal.emit();
          } else {
            this.errorMessage.set(res.message || 'Registration failed.');
          }
        },
        error: (err) => {
          this.isLoading.set(false);
          const msg = err.error?.message || err.error?.error || 'Registration failed. An account with this email may already exist.';
          this.errorMessage.set(msg);
        }
      });
    }
  }

  signInWithGoogle() {
    this.errorMessage.set(null);
    const googleEmail = prompt('Enter your Google Account email:');
    if (!googleEmail) return;

    this.isLoading.set(true);
    this.authService.loginWithGoogle({
      email: googleEmail.trim(),
      fullName: googleEmail.split('@')[0]
    }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        if (res.success) {
          this.closeModal.emit();
        } else {
          this.errorMessage.set(res.message || 'Google authentication failed.');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = err.error?.message || err.error?.error || 'Google authentication failed.';
        this.errorMessage.set(msg);
      }
    });
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.closeModal.emit();
    }
  }
}
