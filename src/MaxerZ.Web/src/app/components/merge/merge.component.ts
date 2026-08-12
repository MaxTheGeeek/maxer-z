import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PdfService, PdfMergeResponse } from '../../services/pdf.service';

@Component({
  selector: 'app-merge',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './merge.component.html',
  styleUrl: './merge.component.scss'
})
export class MergeComponent {
  files = signal<File[]>([]);
  maxFiles = 5;
  errorMessage = signal<string | null>(null);

  isMerging = signal(false);
  progressLogs = signal<string[]>([]);

  mergeResult = signal<PdfMergeResponse | null>(null);
  pdfUrl = signal<SafeResourceUrl | null>(null);

  constructor(
    private pdfService: PdfService,
    private sanitizer: DomSanitizer
  ) {}

  onFileSelected(event: any) {
    const selected: FileList = event.target.files;
    if (!selected || selected.length === 0) return;

    this.errorMessage.set(null);
    const newFiles: File[] = [];

    for (let i = 0; i < selected.length; i++) {
      const file = selected.item(i);
      if (!file) continue;

      if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
        this.errorMessage.set(`File "${file.name}" is not a PDF.`);
        continue;
      }

      if (this.files().length + newFiles.length >= this.maxFiles) {
        this.errorMessage.set(`Maximum limit of ${this.maxFiles} PDF files reached.`);
        break;
      }

      newFiles.push(file);
    }

    if (newFiles.length > 0) {
      this.files.update(current => [...current, ...newFiles]);
    }

    event.target.value = '';
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!event.dataTransfer || !event.dataTransfer.files) return;

    const selected = event.dataTransfer.files;
    this.errorMessage.set(null);
    const newFiles: File[] = [];

    for (let i = 0; i < selected.length; i++) {
      const file = selected.item(i);
      if (!file) continue;

      if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
        this.errorMessage.set(`File "${file.name}" is not a PDF.`);
        continue;
      }

      if (this.files().length + newFiles.length >= this.maxFiles) {
        this.errorMessage.set(`Maximum limit of ${this.maxFiles} PDF files reached.`);
        break;
      }

      newFiles.push(file);
    }

    if (newFiles.length > 0) {
      this.files.update(current => [...current, ...newFiles]);
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
  }

  moveUp(index: number) {
    if (index <= 0) return;
    this.files.update(list => {
      const copy = [...list];
      const temp = copy[index];
      copy[index] = copy[index - 1];
      copy[index - 1] = temp;
      return copy;
    });
  }

  moveDown(index: number) {
    if (index >= this.files().length - 1) return;
    this.files.update(list => {
      const copy = [...list];
      const temp = copy[index];
      copy[index] = copy[index + 1];
      copy[index + 1] = temp;
      return copy;
    });
  }

  removeFile(index: number) {
    this.files.update(list => list.filter((_, i) => i !== index));
    if (this.files().length === 0) {
      this.mergeResult.set(null);
      this.pdfUrl.set(null);
    }
  }

  clearFiles() {
    this.files.set([]);
    this.mergeResult.set(null);
    this.pdfUrl.set(null);
    this.errorMessage.set(null);
  }

  formatSize(sizeInBytes: number): string {
    if (sizeInBytes < 1024) return sizeInBytes + ' B';
    if (sizeInBytes < 1024 * 1024) return (sizeInBytes / 1024).toFixed(1) + ' KB';
    return (sizeInBytes / (1024 * 1024)).toFixed(2) + ' MB';
  }

  executeMerge() {
    if (this.files().length < 1) {
      this.errorMessage.set('Please add at least 1 PDF file to merge.');
      return;
    }

    this.errorMessage.set(null);
    this.isMerging.set(true);
    this.progressLogs.set(['Initiating PDF Merge Engine...']);

    const logInterval = setInterval(() => {
      this.simulateLogs();
    }, 800);

    this.pdfService.mergePdfs(this.files()).subscribe({
      next: (res) => {
        clearInterval(logInterval);
        this.progressLogs.update(logs => [...logs, `✓ Successfully merged ${this.files().length} documents into ${res.pageCount} pages!`, 'Rendering preview...']);
        
        setTimeout(() => {
          this.mergeResult.set(res);
          const dataUri = `data:application/pdf;base64,${res.pdfBase64}`;
          this.pdfUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(dataUri));
          this.isMerging.set(false);
        }, 800);
      },
      error: (err) => {
        clearInterval(logInterval);
        console.error('Merge failed:', err);
        const errorText = err.error?.error || 'Failed to merge PDF files. Ensure files are valid unencrypted PDFs.';
        this.progressLogs.update(logs => [...logs, `✗ Error: ${errorText}`]);
        this.errorMessage.set(errorText);
        this.isMerging.set(false);
      }
    });
  }

  simulateLogs() {
    const logs = [
      `Reading page streams from ${this.files().length} uploaded PDF files...`,
      'Importing page objects and concatenating document trees...',
      'Optimizing PDF cross-reference tables and font dictionaries...',
      'Compiling final merged PDF stream...'
    ];

    const currentLen = this.progressLogs().length;
    if (currentLen - 1 < logs.length) {
      const nextLog = logs[currentLen - 1];
      this.progressLogs.update(l => [...l, nextLog]);
    }
  }

  downloadPdf() {
    const res = this.mergeResult();
    if (!res || !res.pdfBase64) return;

    const byteCharacters = atob(res.pdfBase64);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
      byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: 'application/pdf' });

    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = res.fileName || 'merged_document.pdf';
    link.click();
    URL.revokeObjectURL(link.href);
  }
}
