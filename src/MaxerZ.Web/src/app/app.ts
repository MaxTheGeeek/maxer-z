import { Component, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SettingsService } from './services/settings.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  isSidebarCollapsed = signal(false);
  userName = signal('User Profile');

  constructor(
    private settingsService: SettingsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadThemeAndUser();
  }

  loadThemeAndUser() {
    this.settingsService.getSettings().subscribe({
      next: (settings) => {
        if (settings) {
          // Set user name in sidebar
          if (settings.profile && settings.profile.fullName) {
            this.userName.set(settings.profile.fullName);
          }
          // Set app theme
          const theme = settings.theme || 'dark';
          document.documentElement.setAttribute('data-theme', theme);
        }
      },
      error: (err) => {
        console.error('Failed to load settings in App initialization:', err);
        // Default to dark theme if service fails
        document.documentElement.setAttribute('data-theme', 'dark');
      }
    });
  }

  toggleSidebar() {
    this.isSidebarCollapsed.update(val => !val);
  }

  navigateToSettings(tab: string) {
    this.router.navigate(['/settings'], { queryParams: { tab } });
  }
}
