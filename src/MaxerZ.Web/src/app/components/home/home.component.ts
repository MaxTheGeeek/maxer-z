import { Component, ElementRef, AfterViewInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import * as THREE from 'three';
import gsap from 'gsap';

@Component({
  selector: 'app-home',
  imports: [CommonModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvasContainer', { static: false }) canvasContainer!: ElementRef<HTMLDivElement>;

  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private renderer!: THREE.WebGLRenderer;
  private animFrameId!: number;
  private docGroup!: THREE.Group;
  private particlesMesh!: THREE.Points;

  private mouseX = 0;
  private mouseY = 0;
  private targetX = 0;
  private targetY = 0;

  ngAfterViewInit() {
    this.initThree();
    this.initGsap();
    window.addEventListener('mousemove', this.onMouseMove.bind(this));
    window.addEventListener('resize', this.onWindowResize.bind(this));
  }

  ngOnDestroy() {
    if (this.animFrameId) {
      cancelAnimationFrame(this.animFrameId);
    }
    window.removeEventListener('mousemove', this.onMouseMove.bind(this));
    window.removeEventListener('resize', this.onWindowResize.bind(this));
    if (this.renderer && this.renderer.domElement) {
      this.renderer.dispose();
    }
  }

  private initThree() {
    const container = this.canvasContainer.nativeElement;
    const width = container.clientWidth || 500;
    const height = container.clientHeight || 450;

    // 1. Scene & Camera Setup
    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    this.camera.position.set(0, 0, 7.5);

    // 2. Renderer
    this.renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(this.renderer.domElement);

    // 3. Lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.9);
    this.scene.add(ambientLight);

    const bluePointLight = new THREE.PointLight(0x3b82f6, 3, 20);
    bluePointLight.position.set(5, 5, 5);
    this.scene.add(bluePointLight);

    const purplePointLight = new THREE.PointLight(0x8b5cf6, 3, 20);
    purplePointLight.position.set(-5, -5, 3);
    this.scene.add(purplePointLight);

    const emeraldPointLight = new THREE.PointLight(0x10b981, 2, 20);
    emeraldPointLight.position.set(0, 4, -2);
    this.scene.add(emeraldPointLight);

    // 4. Create 3D Hologram Resume Document Group
    this.docGroup = new THREE.Group();

    // A. Glassmorphic 3D Main Resume Card
    const cardGeometry = new THREE.BoxGeometry(2.8, 3.8, 0.12);
    const cardMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x1e293b,
      metalness: 0.2,
      roughness: 0.1,
      transmission: 0.6,
      transparent: true,
      opacity: 0.85,
      clearcoat: 1.0,
      clearcoatRoughness: 0.1,
      reflectivity: 0.9
    });
    const mainCard = new THREE.Mesh(cardGeometry, cardMaterial);
    this.docGroup.add(mainCard);

    // B. Glowing Metallic Outer Border Frame
    const frameEdges = new THREE.EdgesGeometry(cardGeometry);
    const frameMaterial = new THREE.LineBasicMaterial({ color: 0x60a5fa, linewidth: 2 });
    const wireframe = new THREE.LineSegments(frameEdges, frameMaterial);
    wireframe.scale.set(1.02, 1.02, 1.02);
    this.docGroup.add(wireframe);

    // C. Simulated Holographic Resume Text Lines
    const lineMaterial = new THREE.MeshBasicMaterial({ color: 0x93c5fd });
    const headerLineGeo = new THREE.BoxGeometry(1.6, 0.12, 0.02);
    const headerLine = new THREE.Mesh(headerLineGeo, lineMaterial);
    headerLine.position.set(-0.3, 1.3, 0.08);
    this.docGroup.add(headerLine);

    const subLineMaterial = new THREE.MeshBasicMaterial({ color: 0x818cf8 });
    const subLineGeo = new THREE.BoxGeometry(1.0, 0.08, 0.02);
    const subLine = new THREE.Mesh(subLineGeo, subLineMaterial);
    subLine.position.set(-0.6, 1.0, 0.08);
    this.docGroup.add(subLine);

    // Body lines
    const bodyLineGeo = new THREE.BoxGeometry(2.2, 0.06, 0.02);
    for (let i = 0; i < 5; i++) {
      const lineMesh = new THREE.Mesh(bodyLineGeo, new THREE.MeshBasicMaterial({
        color: i % 2 === 0 ? 0x60a5fa : 0xa7f3d0,
        transparent: true,
        opacity: 0.85
      }));
      lineMesh.position.set(0, 0.5 - i * 0.35, 0.08);
      this.docGroup.add(lineMesh);
    }

    // D. 3D Floating Accent Badges (ATS Score 98% Orb)
    const orbGeo = new THREE.IcosahedronGeometry(0.42, 2);
    const orbMat = new THREE.MeshStandardMaterial({
      color: 0x10b981,
      metalness: 0.8,
      roughness: 0.2,
      emissive: 0x059669,
      emissiveIntensity: 0.4
    });
    const atsOrb = new THREE.Mesh(orbGeo, orbMat);
    atsOrb.position.set(1.6, 1.6, 0.6);
    this.docGroup.add(atsOrb);

    // E. Secondary Floating Ring Around Orb
    const torusGeo = new THREE.TorusGeometry(0.65, 0.03, 16, 50);
    const torusMat = new THREE.MeshBasicMaterial({ color: 0x34d399, wireframe: true });
    const ring = new THREE.Mesh(torusGeo, torusMat);
    ring.position.set(1.6, 1.6, 0.6);
    ring.rotation.x = Math.PI / 3;
    this.docGroup.add(ring);

    this.scene.add(this.docGroup);

    // 5. Ambient 3D Particle Starfield
    const particlesCount = 200;
    const posArray = new Float32Array(particlesCount * 3);
    for (let i = 0; i < particlesCount * 3; i++) {
      posArray[i] = (Math.random() - 0.5) * 12;
    }
    const particlesGeo = new THREE.BufferGeometry();
    particlesGeo.setAttribute('position', new THREE.BufferAttribute(posArray, 3));
    const particlesMat = new THREE.PointsMaterial({
      size: 0.04,
      color: 0x60a5fa,
      transparent: true,
      opacity: 0.6
    });
    this.particlesMesh = new THREE.Points(particlesGeo, particlesMat);
    this.scene.add(this.particlesMesh);

    // Initial slight rotation
    this.docGroup.rotation.y = -0.35;
    this.docGroup.rotation.x = 0.15;

    // Start render loop
    this.animate();
  }

  private animate() {
    this.animFrameId = requestAnimationFrame(this.animate.bind(this));

    // Smooth mouse parallax damping
    this.targetX += (this.mouseX - this.targetX) * 0.05;
    this.targetY += (this.mouseY - this.targetY) * 0.05;

    if (this.docGroup) {
      this.docGroup.rotation.y = -0.35 + this.targetX * 0.5;
      this.docGroup.rotation.x = 0.15 - this.targetY * 0.5;
      this.docGroup.position.y = Math.sin(Date.now() * 0.0015) * 0.15; // Floating levitation
    }

    if (this.particlesMesh) {
      this.particlesMesh.rotation.y = Date.now() * 0.0002;
    }

    this.renderer.render(this.scene, this.camera);
  }

  private onMouseMove(event: MouseEvent) {
    this.mouseX = (event.clientX / window.innerWidth) * 2 - 1;
    this.mouseY = (event.clientY / window.innerHeight) * 2 - 1;
  }

  private onWindowResize() {
    if (!this.canvasContainer || !this.renderer || !this.camera) return;
    const container = this.canvasContainer.nativeElement;
    const width = container.clientWidth;
    const height = container.clientHeight;

    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  }

  private initGsap() {
    // GSAP entrance animation for hero elements
    const tl = gsap.timeline({ defaults: { ease: 'power3.out', duration: 1.0 } });

    tl.from('.hero-badge-pill', { opacity: 0, y: -20 })
      .from('.hero-title-main', { opacity: 0, y: 30 }, '-=0.6')
      .from('.hero-subtitle', { opacity: 0, y: 20 }, '-=0.6')
      .from('.hero-actions-row', { opacity: 0, scale: 0.95 }, '-=0.5')
      .from('.hero-3d-canvas-wrapper', { opacity: 0, scale: 0.8 }, '-=0.8');
  }
}
