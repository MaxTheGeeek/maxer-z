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
  // Tabs selection: 'existing' (My Cover Letter) | 'generate' (AI Generation)
  activeOption = signal('existing');
  
  // Option 1: My Cover Letter fields
  rawRecipientInfo = signal('');
  existingCoverLetterBody = signal('');
  headerAddress = signal('');
  addresses = signal<string[]>([]);

  // Option 2: AI Generation fields
  companyName = signal('');
  position = signal('');
  contactPerson = signal('');
  department = signal('');
  companyLocation = signal('');
  language = signal('en');
  selectedTemplate = signal('template_1'); // 'template_1' | 'template_2'
  templates = signal<any[]>([]);
  coverLetterBody = signal(''); // Holds custom instructions / context
  jobDescription = signal(''); // Holds raw job post

  // Dynamic placeholders
  bodyPlaceholder = signal('Highlight specific skills or background details for generation...');

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
    this.loadTemplates();
  }

  loadTemplates() {
    this.settingsService.getTemplates().subscribe({
      next: (data) => {
        this.templates.set(data);
      },
      error: (err) => {
        console.error('Failed to load templates:', err);
        this.templates.set([
          { id: 'template_1', name: 'Template 1 (Professional Classic)', isCustom: false },
          { id: 'template_2', name: 'Template 2 (Modern Minimalist)', isCustom: false }
        ]);
      }
    });
  }

  loadDraftOrProfile() {
    // Always load addresses list from user settings profile
    this.settingsService.getSettings().subscribe(s => {
      if (s && s.profile) {
        const list = s.profile.addresses || (s.profile.address ? [s.profile.address] : []);
        this.addresses.set(list);
        
        // If no draft is loaded, default the active address to the first configured address
        const draft = this.coverLetterService.getComposeState();
        if (!draft) {
          if (list.length > 0) {
            this.headerAddress.set(list[0]);
          } else {
            this.headerAddress.set('Wiener Straße 20 / 1, 2442 Unterwaltersdorf');
          }
        }
      }
    });

    const draft = this.coverLetterService.getComposeState();
    if (draft) {
      this.activeOption.set(draft.mode || 'existing');
      if (draft.mode === 'existing') {
        this.rawRecipientInfo.set(draft.rawRecipientInfo || '');
        this.existingCoverLetterBody.set(draft.coverLetterBody || '');
      } else {
        this.jobDescription.set(draft.jobDescription || '');
        this.coverLetterBody.set(draft.coverLetterBody || '');
      }
      this.companyName.set(draft.companyName || '');
      this.position.set(draft.position || '');
      this.contactPerson.set(draft.contactPerson || '');
      this.department.set(draft.department || '');
      this.companyLocation.set(draft.companyLocation || '');
      this.language.set(draft.language || 'en');
      this.selectedTemplate.set(draft.selectedTemplate || 'template_1');
      this.headerAddress.set(draft.headerAddress || '');
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
      this.bodyPlaceholder.set('Heben Sie bestimmte Fähigkeiten oder Hintergrundinformationen hervor...');
    } else {
      this.bodyPlaceholder.set('Highlight specific skills or background details for generation...');
    }
  }

  setOption(opt: string) {
    this.activeOption.set(opt);
    this.formError.set(null);
  }

  generate() {
    this.formError.set(null);

    let request: CoverLetterRequest;

    if (this.activeOption() === 'existing') {
      // Validate Option 1: My Cover Letter
      if (!this.rawRecipientInfo().trim()) {
        this.formError.set('Please provide the Job & Recipient Information block.');
        return;
      }
      if (!this.existingCoverLetterBody().trim()) {
        this.formError.set('Please paste your existing cover letter content.');
        return;
      }

      request = {
        mode: 'existing',
        rawRecipientInfo: this.rawRecipientInfo().trim(),
        companyName: '', // Extracted by backend LLM
        position: '',    // Extracted by backend LLM
        companyLocation: '', // Extracted by backend LLM
        language: this.language(),
        selectedTemplate: this.selectedTemplate(),
        headerAddress: this.headerAddress().trim(),
        coverLetterBody: this.existingCoverLetterBody().trim()
      };
    } else {
      // Validate Option 2: AI Generation
      if (!this.companyName().trim() || !this.position().trim() || !this.companyLocation().trim()) {
        this.formError.set('Please fill out all required fields marked with *');
        return;
      }
      if (!this.jobDescription().trim()) {
        this.formError.set('Please paste the job description or requirements.');
        return;
      }

      request = {
        mode: 'generate',
        companyName: this.companyName().trim(),
        position: this.position().trim(),
        contactPerson: this.contactPerson().trim() || undefined,
        department: this.department().trim() || undefined,
        companyLocation: this.companyLocation().trim(),
        language: this.language(),
        selectedTemplate: this.selectedTemplate(),
        headerAddress: this.headerAddress().trim(),
        jobDescription: this.jobDescription().trim(),
        coverLetterBody: this.coverLetterBody().trim()
      };
    }

    // Save current compose state as a draft
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
        const errMsg = err.error?.error || err.message || 'Cascading orchestrator could not process the cover letter. Ensure you have configured at least one LLM API key.';
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
    this.rawRecipientInfo.set('');
    this.existingCoverLetterBody.set('');
    this.companyName.set('');
    this.position.set('');
    this.contactPerson.set('');
    this.department.set('');
    this.companyLocation.set('');
    this.language.set('en');
    this.selectedTemplate.set('template_1');
    this.coverLetterBody.set('');
    this.jobDescription.set('');
    this.updatePlaceholder('en');
    this.formError.set(null);
    this.coverLetterService.setComposeState(null);
    
    this.settingsService.getSettings().subscribe(s => {
      if (s && s.profile && s.profile.address) {
        this.headerAddress.set(s.profile.address);
      } else {
        this.headerAddress.set('Wiener Straße 20 / 1, 2442 Unterwaltersdorf');
      }
    });
  }
}
