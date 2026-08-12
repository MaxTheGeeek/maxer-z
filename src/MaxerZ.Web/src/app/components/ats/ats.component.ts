import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AtsService } from '../../services/ats.service';
import { AtsRequest, AtsResult } from '../../models/models';

@Component({
  selector: 'app-ats',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './ats.component.html',
  styleUrl: './ats.component.scss'
})
export class AtsComponent {
  // Input fields
  resumeText = signal('');
  jobTitle = signal('');
  jobDescription = signal('');
  seniorityLevel = signal('senior');
  targetArchetype = signal('technical');

  // UI / State
  formError = signal<string | null>(null);
  isAnalyzing = signal(false);
  progressLogs = signal<string[]>([]);
  atsResult = signal<AtsResult | null>(null);

  seniorityOptions = [
    { value: 'entry', label: 'Entry Level (0-2 years)' },
    { value: 'mid', label: 'Mid Level (3-5 years)' },
    { value: 'senior', label: 'Senior Level (6-10 years)' },
    { value: 'lead', label: 'Lead / Principal / Executive (10+ years)' }
  ];

  archetypeOptions = [
    { value: 'technical', label: 'Technical / Engineering (Software, Data, Cloud)' },
    { value: 'corporate', label: 'Conservative / Corporate (Finance, Law, Consulting)' },
    { value: 'creative', label: 'Creative / Design (UI/UX, Marketing, Media)' },
    { value: 'sales', label: 'Sales / Business Development (Metrics-first)' },
    { value: 'academic', label: 'Academic / Research (PhD, Publications)' }
  ];

  constructor(private atsService: AtsService) {}

  onFileUpload(event: any) {
    const file = event.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e: any) => {
      const text = e.target.result;
      this.resumeText.set(text);
    };
    reader.readAsText(file);
  }

  analyzeResume() {
    this.formError.set(null);

    if (!this.jobTitle().trim()) {
      this.formError.set('Target job title is required before ATS scoring can begin. Please specify a job title.');
      return;
    }

    if (!this.resumeText().trim()) {
      this.formError.set('Please paste your resume text or upload a resume file.');
      return;
    }

    const request: AtsRequest = {
      resumeText: this.resumeText().trim(),
      jobTitle: this.jobTitle().trim(),
      jobDescription: this.jobDescription().trim() || undefined,
      seniorityLevel: this.seniorityLevel(),
      targetArchetype: this.targetArchetype()
    };

    this.isAnalyzing.set(true);
    this.progressLogs.set(['Initiating 7-Stage ATS Audit Pipeline...']);

    const logInterval = setInterval(() => {
      this.simulateLogs();
    }, 1000);

    this.atsService.analyze(request).subscribe({
      next: (res) => {
        clearInterval(logInterval);
        this.progressLogs.update(logs => [...logs, '✓ Completed 7-Stage Recruiter & QA Audit!', 'Generating score report...']);
        
        setTimeout(() => {
          this.atsResult.set(res);
          this.isAnalyzing.set(false);
        }, 800);
      },
      error: (err) => {
        clearInterval(logInterval);
        console.error('ATS Analysis failed:', err);
        const errMsg = err.error?.error || 'ATS analysis failed. Ensure target job title is provided and API keys are set up.';
        this.formError.set(errMsg);
        this.progressLogs.update(logs => [...logs, `✗ Error: ${errMsg}`]);
        this.isAnalyzing.set(false);
      }
    });
  }

  simulateLogs() {
    const logs = [
      'Stage 1: Ingesting raw text & layout metadata...',
      `Stage 2: Classifying template archetype against "${this.targetArchetype()}" standards...`,
      'Stage 3: Analyzing bullet impact, verb strength, and keyword matches...',
      'Stage 4: Auditing visual formatting, font consistency, and margin integrity...',
      'Stage 5: Checking grammar, tense consistency, and action phrasing...',
      'Stage 6: Calculating weighted scores across 6 core categories...'
    ];

    const currentLen = this.progressLogs().length;
    if (currentLen - 1 < logs.length) {
      const nextLog = logs[currentLen - 1];
      this.progressLogs.update(l => [...l, nextLog]);
    }
  }

  getScoreColor(score: number): string {
    if (score >= 80) return '#10b981'; // Green
    if (score >= 60) return '#f59e0b'; // Yellow
    return '#ef4444'; // Red
  }

  getScoreLabel(score: number): string {
    if (score >= 85) return 'Shortlist Ready';
    if (score >= 70) return 'Competitive';
    if (score >= 50) return 'Needs Optimization';
    return 'High ATS Rejection Risk';
  }
}
