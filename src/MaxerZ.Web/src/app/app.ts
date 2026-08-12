import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SettingsService } from './services/settings.service';
import { AuthService } from './services/auth.service';
import { SeoService } from './services/seo.service';
import { AuthModalComponent } from './components/auth-modal/auth-modal.component';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, AuthModalComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  isSidebarCollapsed = signal(false);
  showAuthModal = signal(false);

  constructor(
    private settingsService: SettingsService,
    public authService: AuthService,
    private seoService: SeoService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.loadThemeAndUser();

    this.route.queryParams.subscribe((params) => {
      if (params['requireAuth'] === 'true') {
        this.showAuthModal.set(true);
      }
    });
  }

  loadThemeAndUser() {
    this.settingsService.getSettings().subscribe({
      next: (settings) => {
        const theme = (settings && settings.theme) ? settings.theme : 'light';
        document.documentElement.setAttribute('data-theme', theme);
      },
      error: () => {
        document.documentElement.setAttribute('data-theme', 'light');
      }
    });
  }

  getUserDisplayName(): string {
    const user = this.authService.currentUser();
    if (user && user.fullName) return user.fullName;
    if (user && user.email) return user.email.split('@')[0];
    return 'Guest User';
  }

  openAuthModal() {
    this.showAuthModal.set(true);
  }

  closeAuthModal() {
    this.showAuthModal.set(false);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/home']);
  }

  navigateToSettings(tab: string) {
    const protectedTabs = ['profile', 'mcp', 'templates'];
    if (protectedTabs.includes(tab) && !this.authService.isLoggedIn()) {
      this.showAuthModal.set(true);
      return;
    }
    this.router.navigate(['/settings'], { queryParams: { tab } });
  }

  navigateToProtected(routePath: string) {
    if (!this.authService.isLoggedIn()) {
      this.showAuthModal.set(true);
      return;
    }
    this.router.navigate([routePath]);
  }
}
