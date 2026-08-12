import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { CoverLetterService } from '../../services/cover-letter.service';
import { ResumeService } from '../../services/resume.service';
import { SettingsService } from '../../services/settings.service';
import { LlmResult, CoverLetterRequest, ResumeResult, ResumeRequest } from '../../models/models';

@Component({
  selector: 'app-preview',
  imports: [CommonModule, FormsModule],
  templateUrl: './preview.component.html',
  styleUrl: './preview.component.scss'
})
export class PreviewComponent implements OnInit {
  // Document Type
  docType = signal<'cover_letter' | 'resume'>('cover_letter');

  // Input request & output results for Cover Letter
  request = signal<CoverLetterRequest | null>(null);
  result = signal<LlmResult | null>(null);
  language = signal('en');

  // Input request & output results for Resume
  resumeReq = signal<ResumeRequest | null>(null);
  resumeRes = signal<ResumeResult | null>(null);

  // Edited letter content fields
  salutationLine = signal('');
  bodyParagraphsText = signal('');
  closingLine = signal('');
  signerName = signal('');

  // Edited resume fields
  summaryText = signal('');
  experienceText = signal('');
  educationText = signal('');
  skillsText = signal('');
  projectsText = signal('');

  // UI States
  pdfUrl = signal<SafeResourceUrl | null>(null);
  isActionLoading = signal(false);
  actionMessage = signal<{ success: boolean; text: string } | null>(null);

  constructor(
    private coverLetterService: CoverLetterService,
    private resumeService: ResumeService,
    private settingsService: SettingsService,
    private router: Router,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit() {
    this.loadPreviewData();
  }

  loadPreviewData() {
    const resumeState = this.resumeService.getPreviewData();
    if (resumeState && resumeState.result) {
      this.docType.set('resume');
      this.resumeReq.set(resumeState.request);
      this.resumeRes.set(resumeState.result);
      this.language.set(resumeState.lang);

      const layout = resumeState.result.layout;
      this.summaryText.set(layout.summaryFormatted || '');
      this.experienceText.set(layout.experienceFormatted || '');
      this.educationText.set(layout.educationFormatted || '');
      this.skillsText.set(layout.skillsFormatted || '');
      this.projectsText.set(layout.projectsFormatted || '');

      this.updatePdfPreview(this.resumeService.getPreviewPdfUrl());
      return;
    }

    const clState = this.coverLetterService.getPreviewData();
    if (clState && clState.result) {
      this.docType.set('cover_letter');
      this.request.set(clState.request);
      this.result.set(clState.result);
      this.language.set(clState.lang);

      const layout = clState.result.layout;
      this.salutationLine.set(layout.salutationLine || '');
      this.bodyParagraphsText.set((layout.bodyParagraphs || []).join('\n\n'));
      this.closingLine.set(layout.closingLine || '');
      this.signerName.set(layout.signerName || '');

      this.updatePdfPreview(this.coverLetterService.getPreviewPdfUrl());
      return;
    }

    // Redirect to compose if no active preview data
    this.router.navigate(['/compose']);
  }

  updatePdfPreview(baseUrl?: string) {
    const urlBase = baseUrl || (this.docType() === 'resume' 
      ? this.resumeService.getPreviewPdfUrl() 
      : this.coverLetterService.getPreviewPdfUrl());
    const url = `${urlBase}?t=${Date.now()}`;
    const safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    this.pdfUrl.set(safeUrl);
  }

  regenerate() {
    this.isActionLoading.set(true);
    this.actionMessage.set(null);

    if (this.docType() === 'resume') {
      const req = this.resumeReq();
      if (!req) return;

      this.resumeService.preview(req).subscribe({
        next: (res) => {
          this.resumeService.setPreviewData(res, req, this.language());
          this.loadPreviewData();
          this.isActionLoading.set(false);
          this.actionMessage.set({ success: true, text: '✓ Re-generated resume text successfully!' });
          setTimeout(() => this.actionMessage.set(null), 3000);
        },
        error: (err) => {
          console.error('Resume regeneration failed:', err);
          this.isActionLoading.set(false);
          this.actionMessage.set({ success: false, text: err.error?.error || 'Failed to re-generate resume.' });
        }
      });
      return;
    }

    const req = this.request();
    if (!req) return;

    this.coverLetterService.preview(req).subscribe({
      next: (res) => {
        this.coverLetterService.setPreviewData(res, req, this.language());
        this.loadPreviewData();
        this.isActionLoading.set(false);
        this.actionMessage.set({ success: true, text: '✓ Re-generated cover letter text successfully!' });
        setTimeout(() => this.actionMessage.set(null), 3000);
      },
      error: (err) => {
        console.error('Regeneration failed:', err);
        this.isActionLoading.set(false);
        this.actionMessage.set({ success: false, text: err.error?.error || 'Failed to re-generate cover letter.' });
      }
    });
  }

  exportPdf() {
    this.isActionLoading.set(true);
    this.actionMessage.set(null);

    if (this.docType() === 'resume') {
      const req = this.resumeReq();
      const res = this.resumeRes();
      if (!req || !res) return;

      const updatedReq: ResumeRequest = {
        ...req,
        summary: this.summaryText().trim(),
        experience: this.experienceText().trim(),
        education: this.educationText().trim(),
        skills: this.skillsText().trim(),
        projects: this.projectsText().trim()
      };

      this.resumeService.export(updatedReq).subscribe({
        next: (exportedResult) => {
          this.isActionLoading.set(false);
          const path = exportedResult.pdfPath || 'export directory';
          this.actionMessage.set({ success: true, text: `✓ Resume PDF exported successfully to: ${path}` });
          if (exportedResult.pdfBase64) {
            this.updatePdfPreview(this.resumeService.getPreviewPdfUrl());
          }
        },
        error: (err) => {
          console.error('Resume export failed:', err);
          this.isActionLoading.set(false);
          this.actionMessage.set({ success: false, text: err.error?.error || 'Failed to export Resume PDF file.' });
        }
      });
      return;
    }

    const req = this.request();
    const res = this.result();
    if (!req || !res) return;

    const paragraphs = this.bodyParagraphsText().split('\n\n')
      .map(p => p.trim())
      .filter(p => p.length > 0);

    const updatedRequest: CoverLetterRequest = {
      ...req,
      companyName: req.companyName || res.layout.companyNameFormatted,
      position: req.position || res.layout.positionFormatted,
      companyLocation: req.companyLocation || res.layout.companyLocation,
      contactPerson: req.contactPerson || res.layout.contactPerson,
      department: req.department || res.layout.department,
      coverLetterBody: JSON.stringify({
        companyNameFormatted: res.layout.companyNameFormatted,
        positionFormatted: res.layout.positionFormatted,
        companyLocation: res.layout.companyLocation,
        contactPerson: res.layout.contactPerson,
        department: res.layout.department,
        salutationLine: this.salutationLine().trim(),
        bodyParagraphs: paragraphs,
        closingLine: this.closingLine().trim(),
        signerName: this.signerName().trim()
      })
    };

    this.coverLetterService.export(updatedRequest).subscribe({
      next: (exportedResult) => {
        this.isActionLoading.set(false);
        const path = exportedResult.pdfPath || 'export directory';
        this.actionMessage.set({ success: true, text: `✓ Cover Letter PDF exported successfully to: ${path}` });
        if (exportedResult.pdfBase64) {
          this.updatePdfPreview(this.coverLetterService.getPreviewPdfUrl());
        }
      },
      error: (err) => {
        console.error('Export failed:', err);
        this.isActionLoading.set(false);
        this.actionMessage.set({ success: false, text: err.error?.error || 'Failed to export PDF file.' });
      }
    });
  }

  goBack() {
    if (this.docType() === 'resume') {
      this.router.navigate(['/resume']);
    } else {
      this.router.navigate(['/compose']);
    }
  }
}

