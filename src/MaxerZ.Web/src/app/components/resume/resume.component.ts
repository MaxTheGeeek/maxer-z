import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ResumeService } from '../../services/resume.service';
import { SettingsService } from '../../services/settings.service';
import { ResumeRequest, LanguageItem } from '../../models/models';

@Component({
  selector: 'app-resume',
  imports: [CommonModule, FormsModule],
  templateUrl: './resume.component.html',
  styleUrl: './resume.component.scss'
})
export class ResumeComponent implements OnInit {
  // Required header fields
  fullName = signal('');
  targetRole = signal('');
  headerAddress = signal('');
  addresses = signal<string[]>([]);

  // Setup & Options
  resumeLanguage = signal('en');
  resumeSelectedTemplate = signal('resume_template_1');
  templates = signal<any[]>([]);

  // Spoken Languages & Proficiency Manager
  languages = signal<LanguageItem[]>([
    { language: 'English', proficiency: 'Upper Intermediate (B2-C1)' }
  ]);
  proficiencyOptions = [
    'Native / Bilingual',
    'Full Professional (C2)',
    'Upper Intermediate (B2-C1)',
    'Intermediate (B1)',
    'Elementary (A1-A2)'
  ];

  // Reorderable Resume Sections
  sectionOrder = signal<string[]>([
    'summary',
    'experience',
    'education',
    'skills',
    'projects',
    'languages'
  ]);

  sectionLabels: Record<string, string> = {
    summary: 'Professional Summary',
    experience: 'Work Experience',
    education: 'Education & Certificates',
    skills: 'Key Skills & Competencies',
    projects: 'Projects & Key Accomplishments',
    languages: 'Languages & Proficiency'
  };

  // Content sections
  resumeSummary = signal('');
  resumeExperience = signal('');
  resumeEducation = signal('');
  resumeSkills = signal('');
  resumeProjects = signal('');

  // Active provider status
  activeProviders = signal<string[]>([]);
  noProvidersWarning = signal(false);

  // Validation state
  formError = signal<string | null>(null);

  // Generation progress
  isGenerating = signal(false);
  progressLogs = signal<string[]>([]);

  constructor(
    private resumeService: ResumeService,
    private settingsService: SettingsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadDraftOrProfile();
    this.loadActiveProviders();
    this.loadTemplates();
  }

  loadTemplates() {
    this.settingsService.getTemplates('resume').subscribe({
      next: (data) => {
        this.templates.set(data);
      },
      error: (err) => {
        console.error('Failed to load resume templates:', err);
        this.templates.set([
          { id: 'resume_template_1', name: 'Template 1 (Executive Resume)', isCustom: false },
          { id: 'resume_template_2', name: 'Template 2 (Modern Clean Resume)', isCustom: false }
        ]);
      }
    });
  }

  addLanguage() {
    this.languages.update(langs => [
      ...langs,
      { language: '', proficiency: 'Upper Intermediate (B2-C1)' }
    ]);
  }

  removeLanguage(index: number) {
    this.languages.update(langs => langs.filter((_, i) => i !== index));
  }

  moveSection(index: number, direction: 'up' | 'down') {
    const list = [...this.sectionOrder()];
    const targetIndex = direction === 'up' ? index - 1 : index + 1;
    if (targetIndex < 0 || targetIndex >= list.length) return;
    const temp = list[index];
    list[index] = list[targetIndex];
    list[targetIndex] = temp;
    this.sectionOrder.set(list);
  }

  loadDraftOrProfile() {
    // Load profile settings for default name, role, and address list
    this.settingsService.getSettings().subscribe(s => {
      if (s && s.profile) {
        if (s.profile.fullName && !this.fullName()) {
          this.fullName.set(s.profile.fullName);
        }
        if (s.profile.role && !this.targetRole()) {
          this.targetRole.set(s.profile.role);
        }

        const list = s.profile.addresses || (s.profile.address ? [s.profile.address] : []);
        this.addresses.set(list);

        const defaultAddr = list.length > 0 ? list[0] : (s.profile.address || 'Musterstraße 1, 1010 Wien');
        
        const resumeDraft = this.resumeService.getResumeComposeState();
        if (!resumeDraft) {
          this.headerAddress.set(defaultAddr);
        }
      }
    });

    // Load Resume draft if present
    const resumeDraft = this.resumeService.getResumeComposeState();
    if (resumeDraft) {
      if (resumeDraft.fullName) this.fullName.set(resumeDraft.fullName);
      if (resumeDraft.targetRole) this.targetRole.set(resumeDraft.targetRole);
      this.resumeSummary.set(resumeDraft.summary || '');
      this.resumeExperience.set(resumeDraft.experience || '');
      this.resumeEducation.set(resumeDraft.education || '');
      this.resumeSkills.set(resumeDraft.skills || '');
      this.resumeProjects.set(resumeDraft.projects || '');
      this.resumeLanguage.set(resumeDraft.language || 'en');
      this.resumeSelectedTemplate.set(resumeDraft.selectedTemplate || 'resume_template_1');
      this.headerAddress.set(resumeDraft.headerAddress || '');

      if (resumeDraft.languages && resumeDraft.languages.length > 0) {
        this.languages.set(resumeDraft.languages);
      }
      if (resumeDraft.sectionOrder && resumeDraft.sectionOrder.length > 0) {
        this.sectionOrder.set(resumeDraft.sectionOrder);
      }
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

  generateResume() {
    this.formError.set(null);

    if (!this.fullName().trim()) {
      this.formError.set('Please provide your Full Name for the resume header.');
      return;
    }

    if (!this.targetRole().trim()) {
      this.formError.set('Please provide your Target Role/Title for the resume header.');
      return;
    }

    if (!this.resumeSummary().trim() && !this.resumeExperience().trim() && !this.resumeSkills().trim()) {
      this.formError.set('Please provide at least a Summary, Work Experience, or Skills section for your Resume.');
      return;
    }

    const validLangs = this.languages().filter(l => l.language.trim().length > 0);

    const request: ResumeRequest = {
      fullName: this.fullName().trim(),
      targetRole: this.targetRole().trim(),
      summary: this.resumeSummary().trim(),
      experience: this.resumeExperience().trim(),
      education: this.resumeEducation().trim(),
      skills: this.resumeSkills().trim(),
      projects: this.resumeProjects().trim(),
      languages: validLangs,
      sectionOrder: this.sectionOrder(),
      language: this.resumeLanguage(),
      selectedTemplate: this.resumeSelectedTemplate(),
      headerAddress: this.headerAddress().trim()
    };

    this.resumeService.setResumeComposeState(request);

    this.isGenerating.set(true);
    this.progressLogs.set(['Initiating Resume LLM formatting pipeline...']);

    const logInterval = setInterval(() => {
      this.simulateResumeLogs();
    }, 1500);

    this.resumeService.preview(request).subscribe({
      next: (res) => {
        clearInterval(logInterval);
        this.progressLogs.update(logs => [...logs, '✓ Resume generated successfully!', 'Caching result and redirecting to Preview...']);

        setTimeout(() => {
          this.resumeService.setPreviewData(res, request, this.resumeLanguage());
          this.isGenerating.set(false);
          this.router.navigate(['/preview']);
        }, 1000);
      },
      error: (err) => {
        clearInterval(logInterval);
        console.error('Resume generation failed:', err);
        const errMsg = err.error?.error || err.message || 'Could not process the resume. Ensure you have configured at least one active LLM API key.';
        this.progressLogs.update(logs => [...logs, `✗ Error: ${errMsg}`]);
        this.formError.set(errMsg);
        this.isGenerating.set(false);
      }
    });
  }

  simulateResumeLogs() {
    const logs = [
      'Extracting professional summary & experience bullet points...',
      'Cascading priority check: OpenRouter -> Groq -> Ollama...',
      'Structuring education, skills, and project highlights...',
      'Applying selected CV layout styling & margins...'
    ];

    const currentLen = this.progressLogs().length;
    if (currentLen - 1 < logs.length) {
      const nextLog = logs[currentLen - 1];
      this.progressLogs.update(l => [...l, nextLog]);
    }
  }

  clearResumeForm() {
    this.resumeSummary.set('');
    this.resumeExperience.set('');
    this.resumeEducation.set('');
    this.resumeSkills.set('');
    this.resumeProjects.set('');
    this.resumeLanguage.set('en');
    this.resumeSelectedTemplate.set('resume_template_1');
    this.formError.set(null);
    this.resumeService.setResumeComposeState(null);

    this.settingsService.getSettings().subscribe(s => {
      if (s && s.profile) {
        if (s.profile.fullName) this.fullName.set(s.profile.fullName);
        if (s.profile.role) this.targetRole.set(s.profile.role);
        const list = s.profile.addresses || (s.profile.address ? [s.profile.address] : []);
        this.headerAddress.set(list[0] || 'Musterstraße 1, 1010 Wien');
      }
    });
  }
}
