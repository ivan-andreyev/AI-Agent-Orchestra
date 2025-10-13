# Phase 6.2: Manual Testing Checklist

**Project**: AI Agent Orchestra
**Phase**: 6.2 - Cross-Browser & Performance Testing
**Status**: READY FOR MANUAL TESTING
**Created**: 2025-10-13

## OVERVIEW

This checklist guides manual browser testing and performance validation for Phase 6.2. All automated testing tools have been created and are ready for use.

## PRE-TESTING SETUP

### 1. Ensure Applications are Running

```powershell
# Terminal 1: Start Orchestra.API
cd src/Orchestra.API
dotnet run --launch-profile http
# Expected: http://localhost:5002

# Terminal 2: Start Orchestra.Web
cd src/Orchestra.Web
dotnet run --launch-profile http
# Expected: http://localhost:5001
```

**Verification**:
- [ ] Orchestra.API accessible at http://localhost:5002
- [ ] Orchestra.Web accessible at http://localhost:5001
- [ ] Both applications start without errors

---

## AUTOMATED TESTING

### 2. Run API Performance Test

```powershell
# Run from project root
pwsh scripts/Test-Phase6-Performance.ps1
```

**Expected Results**:
- [ ] All API endpoints respond within Phase 0 thresholds
- [ ] No performance regressions detected
- [ ] Test output shows "PASS" status

**If Test Fails**:
- Review performance metrics in output
- Check for any system issues (high CPU/memory usage)
- Re-run test to confirm results
- Document any persistent failures in Phase-6.2-Cross-Browser-Performance-Testing.md

---

### 3. Run Browser Performance Test

**Steps**:
1. Open http://localhost:5001/tests/phase6-browser-performance-test.html in Chrome
2. Click "▶️ Run All Tests" button
3. Wait for tests to complete (~5 seconds)
4. Review metrics on page
5. Click "📊 Export Results" to save JSON file

**Expected Metrics**:
- [ ] FCP (First Contentful Paint) < 1000ms
- [ ] LCP (Largest Contentful Paint) < 2500ms
- [ ] TTI (Time to Interactive) < 3000ms
- [ ] DOM Content Loaded < 2000ms
- [ ] CSS Variable Count > 200 (Phase 6.1 design system)
- [ ] JS Heap Size reasonable (< 50 MB)
- [ ] DOM Nodes < 1000

**Export Results**:
- [ ] Save JSON export as `phase6-chrome-performance-results.json`
- [ ] Store in `docs/PLANS/` directory

---

## DESKTOP BROWSER TESTING

### 4. Chrome 120+ (Primary Browser)

**URL**: http://localhost:5001

#### Visual Validation:
- [ ] Home page loads correctly
- [ ] Statistics display readable with proper contrast
- [ ] Sidebar gradient displays correctly (navy to purple)
- [ ] Navigation menu items have proper hover states
- [ ] CoordinatorChat messages display with correct colors
- [ ] All CSS variables resolve (no broken styles)
- [ ] Responsive design works (test at 1920x1080, 1366x768)

#### Functional Validation:
- [ ] Repository selection works
- [ ] Quick Actions accessible
- [ ] Agent list displays
- [ ] Task queue updates
- [ ] All buttons clickable and responsive
- [ ] No console errors

#### Performance Validation:
- [ ] Run browser performance test (step 3)
- [ ] Page loads in < 2 seconds
- [ ] UI interactions responsive (< 100ms feedback)
- [ ] No lag when scrolling
- [ ] Memory usage stable over 5 minutes

**Screenshot**:
- [ ] Capture full-page screenshot: `chrome-desktop-home.png`

---

### 5. Firefox 121+

**URL**: http://localhost:5001

#### Visual Validation:
- [ ] Layout identical to Chrome
- [ ] Colors match Chrome (no Firefox-specific color rendering issues)
- [ ] Fonts render correctly
- [ ] Scrollbar styling appropriate (scrollbar-width, scrollbar-color)
- [ ] CSS Grid layout (if used) renders correctly

#### Functional Validation:
- [ ] All interactive elements work
- [ ] No JavaScript errors in console
- [ ] Form inputs work correctly

#### Performance Validation:
- [ ] Open browser DevTools (F12)
- [ ] Network tab: CSS files load < 200ms
- [ ] Performance tab: Page load < 2 seconds

**Screenshot**:
- [ ] Capture full-page screenshot: `firefox-desktop-home.png`

---

### 6. Edge 120+ (Chromium-based)

**URL**: http://localhost:5001

#### Visual Validation:
- [ ] Identical to Chrome (Chromium engine)
- [ ] Windows-specific rendering correct

#### Functional Validation:
- [ ] All features work identically to Chrome

**Notes**:
- Edge uses same engine as Chrome - comprehensive testing not required
- Focus on Windows-specific rendering quirks only

**Screenshot**:
- [ ] Capture full-page screenshot: `edge-desktop-home.png`

---

### 7. Safari 17+ (macOS/iOS)

**URL**: http://localhost:5001

#### Visual Validation:
- [ ] CSS Custom Properties resolve correctly
- [ ] Gradients render correctly (no webkit- prefix issues)
- [ ] Border radius with overflow hidden works
- [ ] Flexbox layout renders correctly
- [ ] Font rendering matches other browsers

#### Functional Validation:
- [ ] All interactive elements work
- [ ] Touch events work correctly (if testing on iPad)
- [ ] No Safari-specific JavaScript errors

#### Performance Validation:
- [ ] Open Safari Web Inspector
- [ ] Timelines tab: Page load < 2 seconds
- [ ] No excessive style recalculations

**Safari-Specific Checks**:
- [ ] -webkit-appearance not causing issues
- [ ] Safe area insets (iPhone notch) - N/A for desktop
- [ ] No 300ms tap delay (mobile)

**Screenshot**:
- [ ] Capture full-page screenshot: `safari-desktop-home.png`

---

## RESPONSIVE DESIGN TESTING

### 8. Desktop Resolutions

Use Chrome DevTools Device Mode (F12 → Toggle Device Toolbar)

#### 1920x1080 (Full HD)
- [ ] Full sidebar visible
- [ ] Statistics grid displays correctly
- [ ] No horizontal scrolling
- [ ] All components visible without overflow

#### 1366x768 (Laptop)
- [ ] Sidebar adapts appropriately
- [ ] Content remains readable
- [ ] No layout shifts or breaks

---

### 9. Tablet Resolutions

Use Chrome DevTools Device Mode

#### 768x1024 (iPad Portrait)
- [ ] Sidebar collapses or adjusts
- [ ] Touch targets meet 44x44px minimum
- [ ] Statistics grid adapts (2x2 layout)
- [ ] Navigation accessible
- [ ] No horizontal scrolling

#### 1024x768 (iPad Landscape)
- [ ] Full layout visible
- [ ] Sidebar visible
- [ ] Components positioned correctly

**Screenshots**:
- [ ] `tablet-portrait-768x1024.png`
- [ ] `tablet-landscape-1024x768.png`

---

### 10. Mobile Resolutions

Use Chrome DevTools Device Mode

#### 375x667 (iPhone SE)
- [ ] Mobile layout activates
- [ ] Sidebar hidden/collapsible
- [ ] Statistics stack vertically (2x2 grid)
- [ ] Text readable (minimum 16px font size)
- [ ] Buttons minimum 44x44px
- [ ] No horizontal scrolling
- [ ] No content cut off

#### 414x896 (iPhone 11 Pro Max)
- [ ] Similar to iPhone SE but more space
- [ ] Layout adapts smoothly

#### 360x640 (Android Small)
- [ ] Layout functional on small Android devices
- [ ] All content accessible

**Screenshots**:
- [ ] `mobile-iphone-se-375x667.png`
- [ ] `mobile-iphone-11-414x896.png`
- [ ] `mobile-android-360x640.png`

---

## MOBILE BROWSER TESTING (Optional but Recommended)

### 11. iOS Safari 17+ (Real Device)

**Device**: iPhone with iOS 17+

#### Visual Validation:
- [ ] Layout renders correctly
- [ ] No zoom on input focus (input font-size >= 16px)
- [ ] Safe area insets respected (notch/island)
- [ ] Status bar does not overlap content

#### Touch Interaction:
- [ ] Tap targets responsive (< 100ms)
- [ ] No accidental taps due to small targets
- [ ] Scrolling smooth (60fps)
- [ ] Pinch-to-zoom disabled (if intended)

#### Performance:
- [ ] Page loads < 3 seconds on mobile network
- [ ] No jank when scrolling
- [ ] Memory usage reasonable

**Screenshot**:
- [ ] Capture real device screenshot: `ios-safari-real-device.png`

---

### 12. Android Chrome 120+ (Real Device)

**Device**: Android phone with Chrome 120+

#### Visual Validation:
- [ ] Layout identical to iOS (consistency check)
- [ ] No Android-specific rendering issues
- [ ] Keyboard does not cover inputs

#### Touch Interaction:
- [ ] Similar to iOS Safari testing

#### Performance:
- [ ] Similar to iOS Safari testing

**Screenshot**:
- [ ] Capture real device screenshot: `android-chrome-real-device.png`

---

## PERFORMANCE BASELINE VALIDATION

### 13. Compare to Phase 0 Baseline

**Reference Document**: `docs/PLANS/phase-0-performance-baseline.md`

**Metrics to Validate**:

| Metric | Phase 0 Baseline | Phase 6.2 Measured | Change (%) | Threshold | Pass/Fail |
|--------|------------------|-------------------|------------|-----------|-----------|
| API /state | 78.33 ms | ___ ms | ___% | <165 ms | [ ] |
| API /agents | 64.72 ms | ___ ms | ___% | <130 ms | [ ] |
| API /repositories | 81.10 ms | ___ ms | ___% | <165 ms | [ ] |
| Page Load (FCP) | - | ___ ms | - | <1000 ms | [ ] |
| Page Load (LCP) | - | ___ ms | - | <2500 ms | [ ] |
| Page Load (TTI) | - | ___ ms | - | <3000 ms | [ ] |
| CSS Parse Time | - | ___ ms | - | <50 ms | [ ] |
| JS Heap Size | - | ___ MB | - | <50 MB | [ ] |

**Acceptance Criteria**:
- [ ] All API endpoints within Phase 0 thresholds
- [ ] Component render time increase < 10%
- [ ] UI updates < 1 second
- [ ] Memory usage increase < 10%

---

## VISUAL REGRESSION TESTING

### 14. Screenshot Comparison

**Compare Phase 6.1 vs Current**:

Use screenshots to visually compare before/after Phase 6.1 CSS refactoring.

**Components to Compare**:
- [ ] Home page layout
- [ ] Statistics display
- [ ] Navigation menu
- [ ] Coordinator Chat
- [ ] Agent sidebar

**Expected**:
- [ ] Colors consistent (design system palette)
- [ ] Typography consistent (font sizes, weights)
- [ ] Spacing consistent (padding, margins)
- [ ] No layout shifts or broken layouts

**If Visual Regression Detected**:
- Document in Phase-6.2-Cross-Browser-Performance-Testing.md
- Investigate CSS variable resolution issues
- Check for browser-specific CSS bugs

---

## ISSUE TRACKING

### 15. Document Issues Found

For each issue discovered during testing, create an entry in Phase-6.2-Cross-Browser-Performance-Testing.md:

```markdown
**Issue ID**: PHASE6.2-001
**Severity**: Critical / High / Medium / Low
**Browser**: [Browser Name] [Version]
**Screen Size**: [Resolution]
**Component**: [Affected Component]
**Description**: [Detailed description]
**Steps to Reproduce**:
1. [Step 1]
2. [Step 2]
3. [Result]
**Expected Behavior**: [What should happen]
**Actual Behavior**: [What actually happens]
**Screenshot**: [Attach screenshot]
**Fix Required**: Yes / No
```

---

## COMPLETION CRITERIA

### Phase 6.2 Complete When:

- [ ] All automated tests pass (API + Browser performance)
- [ ] Desktop browsers tested (Chrome, Firefox, Edge, Safari)
- [ ] Responsive design validated (desktop, tablet, mobile)
- [ ] Mobile browsers tested (iOS Safari, Android Chrome) - Optional
- [ ] Performance baseline validation complete (<10% regression)
- [ ] All critical/high issues resolved or documented
- [ ] Screenshots captured and stored
- [ ] Test results documented in Phase-6.2-Cross-Browser-Performance-Testing.md
- [ ] No visual regressions detected
- [ ] No functional regressions detected

---

## FINAL DELIVERABLES

### 16. Package Test Results

**Documents to Update**:
1. Phase-6.2-Cross-Browser-Performance-Testing.md
   - [ ] Fill in "Browser Compatibility Results" table
   - [ ] Fill in "Performance Results" table
   - [ ] Fill in "Responsive Design Results" table
   - [ ] Add screenshots
   - [ ] Document any issues found
   - [ ] Mark status as "COMPLETE"

2. UI-Fixes-WorkPlan-2024-09-18.md
   - [ ] Update Phase 6.2 status to "✅ COMPLETED"
   - [ ] Add completion date
   - [ ] Add completion report

3. Create Git Commit:
```bash
git add .
git commit -m "test: Complete Phase 6.2 cross-browser and performance testing

- Validated CSS compatibility across all target browsers
- Confirmed performance within Phase 0 baseline thresholds
- Tested responsive design across desktop, tablet, mobile
- All acceptance criteria met

Testing Results:
- API Performance: [PASS/FAIL]
- Browser Compatibility: [PASS/FAIL]
- Responsive Design: [PASS/FAIL]
- Performance Baseline: [PASS/FAIL]

🤖 Generated with Claude Code
"
```

---

## NOTES

- **Testing Time Estimate**: 2-3 hours for complete manual testing
- **Tools Needed**: Chrome, Firefox, Edge, Safari (if available), mobile devices (optional)
- **Automated Scripts**: Use provided PowerShell and HTML tools for performance validation
- **BrowserStack**: Consider using BrowserStack for Safari/mobile testing if physical devices unavailable

---

**Manual Testing Status**: NOT STARTED
**Last Updated**: 2025-10-13
**Next Step**: Run automated tests, then perform manual browser testing
