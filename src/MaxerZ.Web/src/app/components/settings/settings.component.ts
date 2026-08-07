import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SettingsService } from '../../services/settings.service';
import { AppSettings, McpConfig } from '../../models/models';

@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
  activeTab = signal('api'); // 'api', 'profile', 'mcp', 'app'
  
  // Settings object matching API model structure
  settings = signal<AppSettings>({
    openRouterApiKey: '',
    openRouterModelChain: ['google/gemini-2.5-pro', 'google/gemini-2.5-flash'],
    groqApiKey: '',
    groqModel: 'llama-3.3-70b-versatile',
    ollamaBaseUrl: 'http://localhost:11434',
    ollamaModel: 'llama3',
    providerPriority: ['OpenRouter', 'Groq', 'Ollama', 'RawFallback'],
    theme: 'dark',
    exportDirectory: '',
    profile: {
      fullName: '',
      email: '',
      phone: '',
      linkedInUrl: '',
      gitHubUrl: '',
      websiteUrl: '',
      address: '',
      addresses: [],
      role: '',
      footerText: ''
    }
  });

  mcpConfig = signal<McpConfig>({
    isEnabled: false,
    mcpBaseUrl: '',
    mcpApiKey: ''
  });

  // UI state variables
  isLoading = signal(false);
  saveStatus = signal<{ success: boolean; message: string } | null>(null);
  
  // Test provider states
  testStates = signal<{ [key: string]: { success?: boolean; message?: string; loading?: boolean } }>({
    openrouter: {},
    groq: {},
    ollama: {},
    mcp: {}
  });

  // Available priority options
  allProviders = ['OpenRouter', 'Groq', 'Ollama', 'RawFallback'];

  templatesList = signal<any[]>([]);
  uploadProgress = signal<string | null>(null);
  uploadError = signal<string | null>(null);

  constructor(
    private settingsService: SettingsService,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      if (params['tab']) {
        this.activeTab.set(params['tab']);
      }
    });
    this.loadSettings();
    this.loadTemplatesList();
  }

  loadTemplatesList() {
    this.settingsService.getTemplates().subscribe({
      next: (list) => {
        this.templatesList.set(list);
      },
      error: (err) => {
        console.error('Failed to load templates list:', err);
      }
    });
  }

  onTemplateFileSelected(event: any) {
    const file = event.target.files?.[0];
    if (!file) return;

    if (file.type !== 'application/pdf' && !file.name.endsWith('.pdf')) {
      this.uploadError.set('Only PDF files are allowed.');
      return;
    }

    this.uploadError.set(null);
    this.uploadProgress.set('Uploading...');
    this.settingsService.uploadTemplate(file).subscribe({
      next: () => {
        this.uploadProgress.set('Upload successful!');
        this.loadTemplatesList();
        setTimeout(() => this.uploadProgress.set(null), 3000);
      },
      error: (err) => {
        console.error('Upload failed:', err);
        this.uploadError.set(err.error || 'Upload failed.');
        this.uploadProgress.set(null);
      }
    });
  }

  deleteTemplate(id: string) {
    if (confirm('Are you sure you want to delete this template?')) {
      this.settingsService.deleteTemplate(id).subscribe({
        next: () => {
          this.loadTemplatesList();
        },
        error: (err) => {
          console.error('Failed to delete template:', err);
          alert(err.error || 'Failed to delete template.');
        }
      });
    }
  }

  loadSettings() {
    this.isLoading.set(true);
    this.settingsService.getSettings().subscribe({
      next: (s) => {
        if (s) {
          // Normalize priority list just in case
          if (!s.providerPriority || s.providerPriority.length === 0) {
            s.providerPriority = [...this.allProviders];
          }
          if (s.profile) {
            if (!s.profile.addresses) {
              s.profile.addresses = s.profile.address ? [s.profile.address] : [];
            }
            if (!s.profile.role) s.profile.role = '';
            if (!s.profile.footerText) s.profile.footerText = '';
          }
          this.settings.set(s);
        }
        this.loadMcpSettings();
      },
      error: (err) => {
        console.error('Failed to load settings:', err);
        this.isLoading.set(false);
      }
    });
  }

  loadMcpSettings() {
    this.settingsService.getMcpConfig().subscribe({
      next: (mcp) => {
        if (mcp) {
          this.mcpConfig.set(mcp);
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load MCP config:', err);
        this.isLoading.set(false);
      }
    });
  }

  changeTab(tab: string) {
    this.activeTab.set(tab);
    this.saveStatus.set(null);
  }

  // Move provider order up/down in priority
  movePriority(index: number, direction: 'up' | 'down') {
    const priority = [...this.settings().providerPriority];
    if (direction === 'up' && index > 0) {
      const temp = priority[index];
      priority[index] = priority[index - 1];
      priority[index - 1] = temp;
    } else if (direction === 'down' && index < priority.length - 1) {
      const temp = priority[index];
      priority[index] = priority[index + 1];
      priority[index + 1] = temp;
    }
    this.settings.update(s => ({ ...s, providerPriority: priority }));
  }

  testProvider(providerId: string) {
    this.updateTestState(providerId, { loading: true, success: undefined, message: undefined });
    this.settingsService.testProvider(providerId, this.settings()).subscribe({
      next: (res) => {
        if (res.success) {
          this.updateTestState(providerId, {
            loading: false,
            success: true,
            message: `Connected successfully! Active model: ${res.model || 'Default'}`
          });
        } else {
          this.updateTestState(providerId, {
            loading: false,
            success: false,
            message: res.error || 'Connection failed.'
          });
        }
      },
      error: (err) => {
        this.updateTestState(providerId, {
          loading: false,
          success: false,
          message: err.error?.error || err.message || 'API request error.'
        });
      }
    });
  }

  testMcp() {
    const providerId = 'mcp';
    this.updateTestState(providerId, { loading: true, success: undefined, message: undefined });
    
    // First save the current MCP config so the test uses the new inputs
    this.settingsService.saveMcpConfig(this.mcpConfig()).subscribe({
      next: () => {
        // Run test
        this.settingsService.testMcp().subscribe({
          next: (res) => {
            if (res.success) {
              this.updateTestState(providerId, {
                loading: false,
                success: true,
                message: '✓ MCP Handshake successful.'
              });
            } else {
              this.updateTestState(providerId, {
                loading: false,
                success: false,
                message: '✗ MCP Connection failed.'
              });
            }
          },
          error: (err) => {
            this.updateTestState(providerId, {
              loading: false,
              success: false,
              message: err.error?.error || err.message || 'MCP test failed.'
            });
          }
        });
      },
      error: (err) => {
        this.updateTestState(providerId, {
          loading: false,
          success: false,
          message: 'Failed to save MCP config before testing.'
        });
      }
    });
  }

  addAddress(val: string) {
    if (!val || !val.trim()) return;
    const current = { ...this.settings() };
    if (!current.profile.addresses) {
      current.profile.addresses = [];
    }
    current.profile.addresses.push(val.trim());
    if (!current.profile.address) {
      current.profile.address = val.trim();
    }
    this.settings.set(current);
  }

  removeAddress(index: number) {
    const current = { ...this.settings() };
    if (current.profile.addresses) {
      current.profile.addresses.splice(index, 1);
      if (current.profile.address === current.profile.addresses[index] || !current.profile.addresses.includes(current.profile.address)) {
        current.profile.address = current.profile.addresses[0] || '';
      }
      this.settings.set(current);
    }
  }

  clearCache() {
    this.isLoading.set(true);
    this.saveStatus.set(null);
    this.settingsService.clearCache().subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.saveStatus.set({ success: true, message: res.message || 'Cache cleared successfully!' });
        setTimeout(() => this.saveStatus.set(null), 3000);
      },
      error: (err) => {
        console.error('Failed to clear cache:', err);
        this.isLoading.set(false);
        this.saveStatus.set({ success: false, message: err.error?.error || 'Failed to clear cache.' });
        setTimeout(() => this.saveStatus.set(null), 4000);
      }
    });
  }

  updateTestState(providerId: string, state: { loading?: boolean; success?: boolean; message?: string }) {
    this.testStates.update(s => {
      const updated = { ...s };
      updated[providerId] = { ...updated[providerId], ...state };
      return updated;
    });
  }

  saveAllSettings() {
    this.isLoading.set(true);
    this.saveStatus.set(null);

    // Save app settings
    this.settingsService.saveSettings(this.settings()).subscribe({
      next: () => {
        // Save MCP settings
        this.settingsService.saveMcpConfig(this.mcpConfig()).subscribe({
          next: () => {
            this.isLoading.set(false);
            this.saveStatus.set({ success: true, message: 'Settings saved successfully!' });
            
            // Apply theme changes instantly
            const theme = this.settings().theme || 'dark';
            document.documentElement.setAttribute('data-theme', theme);

            // Clear status alert after 3 seconds
            setTimeout(() => this.saveStatus.set(null), 3000);
          },
          error: (err) => {
            console.error('MCP config save failed:', err);
            this.isLoading.set(false);
            this.saveStatus.set({ success: false, message: 'App settings saved, but MCP configuration save failed.' });
          }
        });
      },
      error: (err) => {
        console.error('Settings save failed:', err);
        this.isLoading.set(false);
        this.saveStatus.set({ success: false, message: err.error?.error || 'Failed to save settings.' });
      }
    });
  }
}
