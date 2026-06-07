import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { CoverLetterService } from '../../services/cover-letter.service';
import { SettingsService } from '../../services/settings.service';
import { LlmResult, CoverLetterRequest } from '../../models/models';

@Component({
  selector: 'app-preview',
  imports: [CommonModule, FormsModule],
  templateUrl: './preview.component.html',
  styleUrl: './preview.component.scss'
})
export class PreviewComponent implements OnInit {
  // Input request & output results
  request = signal<CoverLetterRequest | null>(null);
  result = signal<LlmResult | null>(null);
  language = signal('en');

  // Edited letter content fields
  salutationLine = signal('');
  bodyParagraphsText = signal(''); // Joined by newlines for editing
  closingLine = signal('');
  signerName = signal('');

  // UI States
  pdfUrl = signal<SafeResourceUrl | null>(null);
  isActionLoading = signal(false);
  actionMessage = signal<{ success: boolean; text: string } | null>(null);

  constructor(
    private coverLetterService: CoverLetterService,
    private settingsService: SettingsService,
    private router: Router,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit() {
    this.loadPreviewData();
  }

  loadPreviewData() {
    const state = this.coverLetterService.getPreviewData();
    if (!state || !state.result) {
      // Redirect to compose if no active letter preview data
      this.router.navigate(['/compose']);
      return;
    }

    this.request.set(state.request);
    this.result.set(state.result);
    this.language.set(state.lang);

    const layout = state.result.layout;
    this.salutationLine.set(layout.salutationLine || '');
    this.bodyParagraphsText.set((layout.bodyParagraphs || []).join('\n\n'));
    this.closingLine.set(layout.closingLine || '');
    this.signerName.set(layout.signerName || '');

    this.updatePdfPreview(state.result.pdfBase64);
  }

  updatePdfPreview(base64?: string) {
    if (base64) {
      const dataUrl = `data:application/pdf;base64,${base64}`;
      const safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(dataUrl);
      this.pdfUrl.set(safeUrl);
    } else {
      this.pdfUrl.set(null);
    }
  }

  // Regenerate: call preview API endpoint again
  regenerate() {
    const req = this.request();
    if (!req) return;

    this.isActionLoading.set(true);
    this.actionMessage.set(null);

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

  // Export: call export API endpoint with current edited values
  exportPdf() {
    const req = this.request();
    const res = this.result();
    if (!req || !res) return;

    this.isActionLoading.set(true);
    this.actionMessage.set(null);

    // Prepare updated request payload based on edits
    const paragraphs = this.bodyParagraphsText().split('\n\n')
      .map(p => p.trim())
      .filter(p => p.length > 0);

    // Pack edited fields into the coverLetterBody to persist it
    const updatedRequest: CoverLetterRequest = {
      ...req,
      coverLetterBody: JSON.stringify({
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
        this.actionMessage.set({ success: true, text: `✓ PDF exported successfully to: ${path}` });
        
        // Update the cached results with new PDF base64 if returned
        if (exportedResult.pdfBase64) {
          this.updatePdfPreview(exportedResult.pdfBase64);
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
    this.router.navigate(['/compose']);
  }
}
