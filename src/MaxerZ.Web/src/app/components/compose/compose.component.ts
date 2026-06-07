import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CoverLetterService } from '../../services/cover-letter.service';
import { SettingsService } from '../../services/settings.service';
import { CoverLetterRequest } from '../../models/models';

@Component({
  selector: 'app-compose',
  imports: [CommonModule, FormsModule],
  templateUrl: './compose.component.html',
  styleUrl: './compose.component.scss'
})
export class ComposeComponent implements OnInit {
  // Form fields
  companyName = signal('');
  position = signal('');
  contactPerson = signal('');
  department = signal('');
  companyLocation = signal('');
  language = signal('en');
  coverLetterBody = signal(''); // Holds job description / prompt details

  // Dynamic placeholders
  bodyPlaceholder = signal('Paste the job posting requirements and description here...');

  // Active provider status
  activeProviders = signal<string[]>([]);
  noProvidersWarning = signal(false);

  // Validation state
  formError = signal<string | null>(null);

  // Generation progress
  isGenerating = signal(false);
  progressLogs = signal<string[]>([]);

  constructor(
    private coverLetterService: CoverLetterService,
    private settingsService: SettingsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadDraftOrProfile();
    this.loadActiveProviders();
  }

  loadDraftOrProfile() {
    const draft = this.coverLetterService.getComposeState();
    if (draft) {
      this.companyName.set(draft.companyName || '');
      this.position.set(draft.position || '');
      this.contactPerson.set(draft.contactPerson || '');
      this.department.set(draft.department || '');
      this.companyLocation.set(draft.companyLocation || '');
      this.language.set(draft.language || 'en');
      this.coverLetterBody.set(draft.coverLetterBody || '');
      this.updatePlaceholder(draft.language || 'en');
    }
  }

  loadActiveProviders() {
    this.settingsService.getActiveProviders().subscribe({
      next: (res) => {
        if (res && res.providers) {
          const list = res.providers.map((p: any) => p.label);
          this.activeProviders.set(list);
          this.noProvidersWarning.set(list.length === 0);
        } else {
          this.noProvidersWarning.set(true);
        }
      },
      error: (err) => {
        console.error('Failed to retrieve active providers:', err);
        this.noProvidersWarning.set(true);
      }
    });
  }

  onLanguageChange(lang: string) {
    this.language.set(lang);
    this.updatePlaceholder(lang);
  }

  updatePlaceholder(lang: string) {
    if (lang === 'de') {
      this.bodyPlaceholder.set('Fügen Sie hier die Beschreibung und die Anforderungen der Stellenausschreibung ein...');
    } else {
      this.bodyPlaceholder.set('Paste the job posting requirements and description here...');
    }
  }

  generate() {
    this.formError.set(null);

    // Validate inputs
    if (!this.companyName().trim() || !this.position().trim() || !this.companyLocation().trim() || !this.coverLetterBody().trim()) {
      this.formError.set('Please fill out all required fields marked with *');
      return;
    }

    const request: CoverLetterRequest = {
      companyName: this.companyName().trim(),
      position: this.position().trim(),
      contactPerson: this.contactPerson().trim() || undefined,
      department: this.department().trim() || undefined,
      companyLocation: this.companyLocation().trim(),
      language: this.language(),
      coverLetterBody: this.coverLetterBody().trim()
    };

    // Save current compose state as a draft in case user navigates back
    this.coverLetterService.setComposeState(request);

    this.isGenerating.set(true);
    this.progressLogs.set(['Initiating LLM generation pipeline...']);
    
    const logInterval = setInterval(() => {
      this.simulateLogs();
    }, 1500);

    this.coverLetterService.preview(request).subscribe({
      next: (res) => {
        clearInterval(logInterval);
        this.progressLogs.update(logs => [...logs, '✓ Cover letter generated successfully!', 'Caching result and redirecting to Preview...']);
        
        setTimeout(() => {
          this.coverLetterService.setPreviewData(res, request, this.language());
          this.isGenerating.set(false);
          this.router.navigate(['/preview']);
        }, 1000);
      },
      error: (err) => {
        clearInterval(logInterval);
        console.error('Generation failed:', err);
        const errMsg = err.error?.error || err.message || 'Cascading orchestrator could not generate text. Ensure you have configured at least one LLM API key.';
        this.progressLogs.update(logs => [...logs, `✗ Error: ${errMsg}`]);
        this.formError.set(errMsg);
        this.isGenerating.set(false);
      }
    });
  }

  simulateLogs() {
    const logs = [
      'Querying active provider list...',
      'Cascading priority check: OpenRouter -> Groq -> Ollama...',
      'Connecting to active endpoint...',
      'Generating structured response object...',
      'Formatting letter margins and paragraph templates...'
    ];
    
    const currentLen = this.progressLogs().length;
    if (currentLen - 1 < logs.length) {
      const nextLog = logs[currentLen - 1];
      this.progressLogs.update(l => [...l, nextLog]);
    }
  }

  clearForm() {
    this.companyName.set('');
    this.position.set('');
    this.contactPerson.set('');
    this.department.set('');
    this.companyLocation.set('');
    this.language.set('en');
    this.coverLetterBody.set('');
    this.updatePlaceholder('en');
    this.formError.set(null);
    this.coverLetterService.setComposeState(null);
  }
}
