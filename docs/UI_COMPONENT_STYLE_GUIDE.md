# UI Component Style Guide

## Philosophy

StageFright Community embraces a **clean, simple, modern design** philosophy that prioritizes clarity, usability, and information density. Every visual element should serve a purpose; unnecessary clutter is eliminated without sacrificing accessibility or usability.

**Key Principles:**
- Clean and intentional design
- Minimal but purposeful whitespace
- Modern, professional aesthetics
- Consistent visual language
- Accessible to all users
- Responsive and performant

---

## Color Palette

### Primary Colors

```
Primary Blue: #2563EB
  Light: #3B82F6
  Dark:  #1D4ED8
  
Secondary Gray: #6B7280
  Light: #E5E7EB
  Lighter: #F3F4F6
  
Success: #10B981
Warning: #F59E0B
Danger:  #EF4444
Info:    #0EA5E9
```

### Typography

```
Font Family: System font stack (Segoe UI, -apple-system, sans-serif)

Heading 1: 28px, Bold (700), line-height 1.2, color #111827
Heading 2: 24px, Bold (700), line-height 1.25, color #111827
Heading 3: 20px, Semi-bold (600), line-height 1.3, color #111827

Body:      16px, Regular (400), line-height 1.5, color #374151
Small:     14px, Regular (400), line-height 1.43, color #6B7280
Caption:   12px, Regular (400), line-height 1.33, color #9CA3AF
```

### Spacing Scale

```
xs: 4px
sm: 8px
md: 12px
lg: 16px
xl: 24px
2xl: 32px
3xl: 48px
```

All padding, margins, and gaps should use multiples of these values.

---

## Layout Principles

### Compact Grids

Use tight, information-dense layouts with minimal whitespace. Default to compact spacing:

```razor
<!-- ❌ EXCESSIVE WHITESPACE -->
<div style="padding: 40px; gap: 40px;">
    <div>Item 1</div>
    <div>Item 2</div>
</div>

<!-- ✅ COMPACT, INTENTIONAL SPACING -->
<div style="padding: 16px; gap: 12px;">
    <div>Item 1</div>
    <div>Item 2</div>
</div>
```

### Responsive Containers

```css
.page-container {
    max-width: 1400px;
    margin: 0 auto;
    padding: 16px;
}

.grid-layout {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 12px;
}

.card {
    background: white;
    border: 1px solid #E5E7EB;
    border-radius: 6px;
    padding: 12px;
}
```

### Section Dividers

Use subtle dividers instead of heavy whitespace:

```css
/* ✅ GOOD: Subtle divider */
.section {
    border-bottom: 1px solid #E5E7EB;
    padding-bottom: 12px;
    margin-bottom: 12px;
}

/* ❌ BAD: Excessive gap */
.section {
    margin-bottom: 40px;
}
```

---

## Component Library

### Form Controls

#### Input Fields

```razor
<div class="form-group">
    <label for="name">Name</label>
    <input 
        type="text" 
        id="name" 
        class="form-control"
        placeholder="Enter full name"
        aria-label="Member name"
    />
    <span class="form-error" id="name-error">@ErrorMessage</span>
</div>
```

```css
.form-control {
    width: 100%;
    padding: 8px 12px;
    border: 1px solid #D1D5DB;
    border-radius: 4px;
    font-size: 14px;
    line-height: 1.5;
    transition: border-color 0.15s, box-shadow 0.15s;
}

.form-control:focus {
    outline: none;
    border-color: #2563EB;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.form-control:disabled {
    background-color: #F3F4F6;
    color: #9CA3AF;
}

.form-error {
    display: block;
    color: #EF4444;
    font-size: 12px;
    margin-top: 4px;
}
```

#### Buttons

```razor
<!-- Primary Button -->
<button class="btn btn-primary">Save</button>

<!-- Secondary Button -->
<button class="btn btn-secondary">Cancel</button>

<!-- Danger Button -->
<button class="btn btn-danger">Delete</button>

<!-- Button Group -->
<div class="button-group">
    <button class="btn btn-secondary">Cancel</button>
    <button class="btn btn-primary">Save</button>
</div>
```

```css
.btn {
    padding: 8px 16px;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.15s, opacity 0.15s;
    display: inline-flex;
    align-items: center;
    gap: 8px;
}

.btn-primary {
    background-color: #2563EB;
    color: white;
}

.btn-primary:hover {
    background-color: #1D4ED8;
}

.btn-primary:active {
    opacity: 0.95;
}

.btn-secondary {
    background-color: #E5E7EB;
    color: #374151;
}

.btn-secondary:hover {
    background-color: #D1D5DB;
}

.btn-danger {
    background-color: #EF4444;
    color: white;
}

.button-group {
    display: flex;
    gap: 8px;
    justify-content: flex-end;
}
```

### Tables

```razor
<table class="table table-striped">
    <thead>
        <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Status</th>
            <th class="text-right">Actions</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var member in members)
        {
            <tr>
                <td>@member.Name</td>
                <td>@member.Email</td>
                <td><Badge Status="@member.Status" /></td>
                <td class="text-right">
                    <IconButton Icon="edit" OnClick="@(() => Edit(member))" />
                    <IconButton Icon="delete" OnClick="@(() => Delete(member))" />
                </td>
            </tr>
        }
    </tbody>
</table>
```

```css
.table {
    width: 100%;
    border-collapse: collapse;
    font-size: 14px;
    background: white;
}

.table th {
    background-color: #F3F4F6;
    padding: 12px;
    text-align: left;
    font-weight: 600;
    color: #374151;
    border-bottom: 1px solid #E5E7EB;
}

.table td {
    padding: 12px;
    border-bottom: 1px solid #E5E7EB;
}

.table tbody tr:hover {
    background-color: #F9FAFB;
}

.table-striped tbody tr:nth-child(odd) {
    background-color: #F9FAFB;
}

.table-striped tbody tr:nth-child(odd):hover {
    background-color: #F3F4F6;
}

.text-right {
    text-align: right;
}
```

### Cards

```razor
<div class="card">
    <div class="card-header">
        <h3>Member Statistics</h3>
    </div>
    <div class="card-body">
        <div class="stat-grid">
            <div class="stat">
                <div class="stat-value">245</div>
                <div class="stat-label">Total Members</div>
            </div>
            <div class="stat">
                <div class="stat-value">18</div>
                <div class="stat-label">Outstanding Fees</div>
            </div>
        </div>
    </div>
</div>
```

```css
.card {
    background: white;
    border: 1px solid #E5E7EB;
    border-radius: 6px;
    overflow: hidden;
}

.card-header {
    padding: 12px 16px;
    border-bottom: 1px solid #E5E7EB;
    background-color: #F9FAFB;
}

.card-header h3 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
    color: #111827;
}

.card-body {
    padding: 16px;
}

.stat-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: 12px;
}

.stat {
    background-color: #F3F4F6;
    padding: 12px;
    border-radius: 4px;
    text-align: center;
}

.stat-value {
    font-size: 28px;
    font-weight: 700;
    color: #2563EB;
}

.stat-label {
    font-size: 12px;
    color: #6B7280;
    margin-top: 4px;
}
```

### Badges and Status Indicators

```razor
<Badge Status="Active" />
<Badge Status="Inactive" />
<Badge Type="success">Paid</Badge>
<Badge Type="warning">Pending</Badge>
<Badge Type="danger">Outstanding</Badge>
```

```css
.badge {
    display: inline-block;
    padding: 4px 8px;
    border-radius: 12px;
    font-size: 12px;
    font-weight: 500;
    white-space: nowrap;
}

.badge-success {
    background-color: #D1FAE5;
    color: #065F46;
}

.badge-warning {
    background-color: #FEF3C7;
    color: #92400E;
}

.badge-danger {
    background-color: #FEE2E2;
    color: #991B1B;
}

.badge-info {
    background-color: #E0F2FE;
    color: #0C4A6E;
}
```

### Empty States

```razor
<div class="empty-state">
    <div class="empty-state-icon">
        <Icon Name="inbox" />
    </div>
    <h3>No members found</h3>
    <p>Get started by adding your first member to the system.</p>
    <button class="btn btn-primary" onclick="location.href='/members/new'">
        Add Member
    </button>
</div>
```

```css
.empty-state {
    text-align: center;
    padding: 48px 16px;
    background-color: #F9FAFB;
    border-radius: 6px;
    border: 1px dashed #D1D5DB;
}

.empty-state-icon {
    font-size: 48px;
    color: #D1D5DB;
    margin-bottom: 16px;
}

.empty-state h3 {
    margin: 0 0 8px 0;
    color: #374151;
    font-size: 18px;
}

.empty-state p {
    margin: 0 0 16px 0;
    color: #6B7280;
    font-size: 14px;
}
```

### Loading Indicators

```razor
<!-- Spinner -->
<div class="spinner"></div>

<!-- Skeleton Loader -->
<div class="skeleton-line" style="width: 100%;"></div>
<div class="skeleton-line" style="width: 80%;"></div>
```

```css
.spinner {
    width: 32px;
    height: 32px;
    border: 3px solid #E5E7EB;
    border-top-color: #2563EB;
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}

.skeleton-line {
    height: 12px;
    background: linear-gradient(
        90deg,
        #E5E7EB 0%,
        #F3F4F6 50%,
        #E5E7EB 100%
    );
    background-size: 200% 100%;
    border-radius: 4px;
    margin-bottom: 8px;
    animation: loading 1.5s infinite;
}

@keyframes loading {
    0% { background-position: 200% 0; }
    100% { background-position: -200% 0; }
}
```

### Alerts and Notifications

```razor
<div class="alert alert-success">
    <span class="alert-icon">✓</span>
    <span>Member created successfully!</span>
    <button class="alert-close" onclick="this.parentElement.style.display='none';">×</button>
</div>

<div class="alert alert-warning">
    <span class="alert-icon">⚠</span>
    <span>3 members have outstanding fees</span>
</div>

<div class="alert alert-danger">
    <span class="alert-icon">✕</span>
    <span>Failed to save member: Email already exists</span>
</div>
```

```css
.alert {
    padding: 12px 16px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 14px;
    margin-bottom: 12px;
}

.alert-success {
    background-color: #D1FAE5;
    color: #065F46;
    border: 1px solid #6EE7B7;
}

.alert-warning {
    background-color: #FEF3C7;
    color: #92400E;
    border: 1px solid #FCD34D;
}

.alert-danger {
    background-color: #FEE2E2;
    color: #991B1B;
    border: 1px solid #FCA5A5;
}

.alert-icon {
    font-weight: 700;
}

.alert-close {
    background: none;
    border: none;
    color: inherit;
    cursor: pointer;
    font-size: 18px;
    margin-left: auto;
    padding: 0;
}
```

---

## Dashboard Tiles

Dashboard tiles are the primary way features expose their functionality. Each tile should be compact, focused, and provide immediate value.

### Tile Structure

```razor
@* DashboardTile.cs *@
public class MembersDashboardTile : IDashboardTile
{
    public string Title => "Members";
    public int Order => 1;
    public string Icon => "users";
    
    public async Task<IDashboardTileContent> GetContentAsync()
    {
        var activeCount = await _memberService.GetActiveMemberCountAsync();
        var pendingFees = await _memberService.GetPendingFeeCountAsync();
        
        return new MembersTileContent
        {
            ActiveMembers = activeCount,
            PendingFees = pendingFees,
            RecentMembers = await _memberService.GetRecentMembersAsync(5)
        };
    }
}

@* MembersTile.razor *@
<div class="dashboard-tile">
    <div class="tile-header">
        <h3>Members</h3>
        <a href="/members" class="tile-link">View all →</a>
    </div>
    
    <div class="tile-stats">
        <div class="stat-box">
            <div class="stat-number">@Content.ActiveMembers</div>
            <div class="stat-name">Active</div>
        </div>
        <div class="stat-box alert">
            <div class="stat-number">@Content.PendingFees</div>
            <div class="stat-name">Pending Fees</div>
        </div>
    </div>
    
    <div class="tile-list">
        @foreach (var member in Content.RecentMembers)
        {
            <div class="list-item">
                <span>@member.Name</span>
                <span class="status-badge">@member.Status</span>
            </div>
        }
    </div>
    
    <button class="btn btn-secondary btn-sm" onclick="location.href='/members/new'">
        Add Member
    </button>
</div>
```

```css
.dashboard-tile {
    background: white;
    border: 1px solid #E5E7EB;
    border-radius: 6px;
    padding: 12px;
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.tile-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid #E5E7EB;
    padding-bottom: 8px;
}

.tile-header h3 {
    margin: 0;
    font-size: 16px;
    font-weight: 600;
}

.tile-link {
    color: #2563EB;
    text-decoration: none;
    font-size: 13px;
}

.tile-link:hover {
    text-decoration: underline;
}

.tile-stats {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 8px;
}

.stat-box {
    background-color: #F3F4F6;
    padding: 8px;
    border-radius: 4px;
    text-align: center;
}

.stat-box.alert {
    background-color: #FEF3C7;
}

.stat-number {
    font-size: 20px;
    font-weight: 700;
    color: #2563EB;
}

.stat-name {
    font-size: 11px;
    color: #6B7280;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.tile-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
}

.list-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px;
    border-radius: 4px;
    background-color: #F9FAFB;
    font-size: 13px;
}

.list-item:hover {
    background-color: #F3F4F6;
}

.btn-sm {
    padding: 6px 12px;
    font-size: 13px;
    width: 100%;
}
```

---

## Navigation Menu

The main navigation menu is the primary way users access application features. Menu items can include icons and badges for visual communication of status and available actions.

### Menu Structure

```html
<nav class="main-navigation">
    <div class="nav-brand">
        <span>StageFright</span>
    </div>
    
    <ul class="nav-menu">
        <!-- Dashboard (always first) -->
        <li class="nav-item">
            <a href="/dashboard" class="nav-link">
                <i class="icon icon-home"></i>
                <span>Dashboard</span>
            </a>
        </li>
        
        <!-- Module menu items (sorted by DisplayOrder) -->
        <li class="nav-item">
            <a href="/members" class="nav-link">
                <i class="icon icon-users"></i>
                <span>Members</span>
            </a>
            
            <!-- Submenu items -->
            <ul class="nav-submenu">
                <li class="nav-subitem">
                    <a href="/members/list" class="nav-sublink">Active Members</a>
                </li>
                <li class="nav-subitem">
                    <a href="/members/pending" class="nav-sublink">
                        Pending Approval
                        <span class="badge">3</span>
                    </a>
                </li>
                <li class="nav-subitem">
                    <a href="/members/new" class="nav-sublink">Add Member</a>
                </li>
            </ul>
        </li>
        
        <li class="nav-item">
            <a href="/events" class="nav-link">
                <i class="icon icon-calendar"></i>
                <span>Events</span>
            </a>
        </li>
        
        <!-- Settings (always last) -->
        <li class="nav-item nav-settings">
            <a href="/settings" class="nav-link">
                <i class="icon icon-cog"></i>
                <span>Settings</span>
            </a>
        </li>
    </ul>
</nav>
```

### Navigation Styles

```css
/* Main navigation container */
.main-navigation {
    background-color: #FFFFFF;
    border-bottom: 1px solid #E5E7EB;
    padding: 0;
    position: sticky;
    top: 0;
    z-index: 100;
}

/* Brand/logo area */
.nav-brand {
    padding: 12px 16px;
    border-bottom: 1px solid #E5E7EB;
    font-size: 16px;
    font-weight: 600;
    color: #1F2937;
    display: flex;
    align-items: center;
    gap: 8px;
}

/* Main menu list */
.nav-menu {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
}

/* Menu item */
.nav-item {
    position: relative;
}

.nav-item .nav-link {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 12px 16px;
    color: #374151;
    text-decoration: none;
    font-size: 14px;
    font-weight: 500;
    transition: all 0.2s;
    cursor: pointer;
}

.nav-item .nav-link:hover {
    background-color: #F3F4F6;
    color: #2563EB;
}

.nav-item .nav-link.active {
    background-color: #EFF6FF;
    color: #2563EB;
    border-left: 3px solid #2563EB;
    padding-left: 13px;
}

/* Icon in menu */
.nav-link .icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
}

/* Badge on menu items */
.nav-link .badge {
    margin-left: auto;
    background-color: #EF4444;
    color: white;
    border-radius: 10px;
    padding: 2px 6px;
    font-size: 11px;
    font-weight: 600;
}

/* Submenu */
.nav-submenu {
    list-style: none;
    margin: 0;
    padding: 0;
    background-color: #F9FAFB;
    max-height: 0;
    overflow: hidden;
    transition: max-height 0.3s ease-out;
}

.nav-item:hover .nav-submenu,
.nav-item.expanded .nav-submenu {
    max-height: 500px;
}

/* Submenu item */
.nav-subitem {
    border-left: 3px solid transparent;
}

.nav-subitem .nav-sublink {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 16px 10px 24px;
    color: #6B7280;
    text-decoration: none;
    font-size: 13px;
    transition: all 0.2s;
}

.nav-subitem .nav-sublink:hover {
    background-color: #F3F4F6;
    color: #2563EB;
}

.nav-subitem .nav-sublink.active {
    color: #2563EB;
    background-color: #EFF6FF;
    border-left-color: #2563EB;
}

.nav-subitem .badge {
    background-color: #F97316;
    color: white;
    border-radius: 8px;
    padding: 2px 6px;
    font-size: 11px;
    font-weight: 600;
}

/* Settings menu item (always last) */
.nav-item.nav-settings {
    border-top: 1px solid #E5E7EB;
    margin-top: 8px;
}

.nav-item.nav-settings .nav-link {
    color: #374151;
}

.nav-item.nav-settings .nav-link:hover {
    color: #2563EB;
}

/* Responsive: Collapse menu on small screens */
@media (max-width: 768px) {
    .main-navigation {
        position: fixed;
        left: 0;
        top: 0;
        bottom: 0;
        width: 250px;
        max-height: 100vh;
        overflow-y: auto;
        background: white;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
        z-index: 200;
    }
    
    .nav-link,
    .nav-sublink {
        padding-left: 16px;
    }
}
```

### Menu with Icons

Icons enhance menu item recognition. Use consistent icon names and sizes:

```css
/* Icon sizing */
.icon {
    display: inline-block;
    width: 18px;
    height: 18px;
    flex-shrink: 0;
}

.icon-home::before { content: "🏠"; }
.icon-users::before { content: "👥"; }
.icon-calendar::before { content: "📅"; }
.icon-dollar-sign::before { content: "💰"; }
.icon-cog::before { content: "⚙️"; }
.icon-check::before { content: "✓"; }
```

Or use a proper icon library (FontAwesome, Material Icons, etc.):

```html
<!-- FontAwesome example -->
<i class="fas fa-users"></i>
<i class="fas fa-calendar"></i>
<i class="fas fa-dollar-sign"></i>
```

### Active State Indicator

```razor
@* Navigation component with active state *@
@page "/"
@inject NavigationManager NavManager

@foreach (var item in menuItems)
{
    var isActive = NavManager.Uri.Contains(item.Route);
    <a href="@item.Route" class="nav-link @(isActive ? "active" : "")">
        @item.Title
    </a>
}
```

### Menu Item Badge Examples

```html
<!-- Pending items count -->
<a href="/members/pending" class="nav-link">
    <i class="icon icon-users"></i>
    <span>Members</span>
    <span class="badge">3</span>
</a>

<!-- New notifications -->
<a href="/events" class="nav-link">
    <i class="icon icon-calendar"></i>
    <span>Events</span>
    <span class="badge">5</span>
</a>

<!-- Urgent status -->
<a href="/finances/invoices" class="nav-link">
    <i class="icon icon-dollar-sign"></i>
    <span>Invoices</span>
    <span class="badge" style="background-color: #EF4444;">2</span>
</a>
```

---

## Responsive Design

### Breakpoints

```css
/* Mobile First */
@media (min-width: 640px)  { /* sm */ }
@media (min-width: 768px)  { /* md */ }
@media (min-width: 1024px) { /* lg */ }
@media (min-width: 1280px) { /* xl */ }
@media (min-width: 1536px) { /* 2xl */ }
```

### Responsive Grid Example

```css
.grid-layout {
    display: grid;
    grid-template-columns: 1fr;
    gap: 12px;
}

@media (min-width: 768px) {
    .grid-layout {
        grid-template-columns: repeat(2, 1fr);
    }
}

@media (min-width: 1024px) {
    .grid-layout {
        grid-template-columns: repeat(3, 1fr);
    }
}
```

---

## Accessibility

### Semantic HTML

```razor
<!-- ✅ GOOD: Semantic HTML -->
<nav>
    <ul>
        <li><a href="/members">Members</a></li>
        <li><a href="/finances">Finances</a></li>
    </ul>
</nav>

<!-- ❌ BAD: Div soup -->
<div>
    <div onclick="navigate('/members')">Members</div>
</div>
```

### ARIA Labels

```razor
<button aria-label="Delete member" onclick="@DeleteMember">
    <Icon Name="trash" />
</button>

<input 
    aria-label="Search members" 
    placeholder="Search..."
    aria-describedby="search-help"
/>
<small id="search-help">Search by name or email</small>
```

### Color Contrast

- All text must have WCAG AA contrast ratio (4.5:1 for normal text, 3:1 for large text)
- Don't rely on color alone; use patterns or labels
- Use accessible color combinations

### Keyboard Navigation

```razor
<!-- Tab order must be logical -->
<form>
    <input type="text" />
    <input type="email" />
    <button type="submit">Save</button>
</form>

<!-- Custom components need tabindex management -->
<div 
    role="button"
    tabindex="0"
    @onkeydown="@((KeyboardEventArgs e) => e.Key == 'Enter' ? OnClick() : Task.CompletedTask)"
    @onclick="OnClick"
>
    Click or press Enter
</div>
```

---

## Performance Tips

### Images

- Use appropriately sized images
- Lazy-load images below the fold
- Use SVG for icons

```razor
<img 
    src="member.jpg" 
    alt="Member photo"
    loading="lazy"
    width="100"
    height="100"
/>
```

### CSS

- Use CSS isolation for component styling
- Minimize repaints and reflows
- Avoid expensive pseudo-selectors

```razor
@* MembersPage.razor.css *@
.members-list {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 12px;
}
```

### JavaScript

- Avoid inline JavaScript
- Use Blazor event handlers instead of onclick strings
- Defer non-critical scripts

---

## Common Patterns

### Form with Validation

```razor
<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    
    <div class="form-group">
        <label>Name *</label>
        <InputText @bind-Value="model.Name" class="form-control" />
        <ValidationMessage For="@(() => model.Name)" />
    </div>
    
    <div class="form-group">
        <label>Email *</label>
        <InputText @bind-Value="model.Email" class="form-control" />
        <ValidationMessage For="@(() => model.Email)" />
    </div>
    
    <div class="button-group">
        <button type="button" class="btn btn-secondary" @onclick="OnCancel">Cancel</button>
        <button type="submit" class="btn btn-primary">Save</button>
    </div>
</EditForm>
```

### Modal Dialog

```razor
@if (showModal)
{
    <div class="modal-overlay" @onclick="@(() => showModal = false)">
        <div class="modal" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h2>Confirm Action</h2>
                <button class="modal-close" @onclick="@(() => showModal = false)">×</button>
            </div>
            <div class="modal-body">
                Are you sure you want to delete this member?
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" @onclick="@(() => showModal = false)">
                    Cancel
                </button>
                <button class="btn btn-danger" @onclick="ConfirmDelete">
                    Delete
                </button>
            </div>
        </div>
    </div>
}
```

```css
.modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal {
    background: white;
    border-radius: 6px;
    max-width: 500px;
    width: 90%;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 16px;
    border-bottom: 1px solid #E5E7EB;
}

.modal-body {
    padding: 16px;
}

.modal-footer {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding: 16px;
    border-top: 1px solid #E5E7EB;
}
```

### Settings Forms and Tabs

Settings tabs use a consistent pattern with clear visual hierarchy and validation:

```razor
@* Features/Members/UI/Components/MembersSettingsTab.razor *@
@implements IAsyncDisposable
@inject IMembersSettingsService SettingsService
@inject ILogger<MembersSettingsTab> Logger

<div class="settings-tab">
    @if (isLoading)
    {
        <div class="spinner-container">
            <div class="spinner"></div>
            <p>Loading settings...</p>
        </div>
    }
    else if (settings != null)
    {
        <EditForm Model="@settings" OnValidSubmit="@HandleSave">
            <DataAnnotationsValidator />
            
            <div class="settings-section">
                <h3>Member Configuration</h3>
                <p class="section-description">Configure default member settings and preferences</p>
                
                <div class="form-group">
                    <label for="defaultStatus">Default Member Status</label>
                    <InputSelect @bind-Value="settings.DefaultMemberStatus" 
                                id="defaultStatus" class="form-control">
                        <option value="Active">Active</option>
                        <option value="Inactive">Inactive</option>
                        <option value="Pending">Pending Review</option>
                    </InputSelect>
                    <ValidationMessage For="@(() => settings.DefaultMemberStatus)" />
                    <small class="form-text">New members will have this status by default</small>
                </div>
                
                <div class="form-group">
                    <label for="autoArchiveDays">Auto-Archive Inactive After (days)</label>
                    <InputNumber @bind-Value="settings.AutoArchiveInactiveDays" 
                                id="autoArchiveDays" class="form-control" min="1" max="1095" />
                    <ValidationMessage For="@(() => settings.AutoArchiveInactiveDays)" />
                    <small class="form-text">Members inactive for this many days will be automatically archived</small>
                </div>
            </div>
            
            <div class="settings-section">
                <h3>Contact Preferences</h3>
                
                <div class="form-group">
                    <label>
                        <InputCheckbox @bind-Value="settings.SendEmailNotifications" />
                        Send email notifications to members
                    </label>
                </div>
                
                <div class="form-group">
                    <label>
                        <InputCheckbox @bind-Value="settings.SendSmsReminders" />
                        Send SMS reminders for upcoming events
                    </label>
                </div>
            </div>
            
            @if (!string.IsNullOrEmpty(errorMessage))
            {
                <div class="alert alert-danger">
                    <strong>Error:</strong> @errorMessage
                </div>
            }
            
            @if (showSuccessMessage)
            {
                <div class="alert alert-success">
                    Settings saved successfully
                </div>
            }
            
            <div class="settings-actions">
                <button type="button" class="btn btn-secondary" disabled="@isSaving" @onclick="OnCancel">
                    Cancel
                </button>
                <button type="submit" class="btn btn-primary" disabled="@isSaving">
                    @if (isSaving)
                    {
                        <span class="spinner-sm"></span>
                        <span>Saving...</span>
                    }
                    else
                    {
                        <span>Save Settings</span>
                    }
                </button>
            </div>
        </EditForm>
    }
</div>

@code {
    [CascadingParameter]
    private SettingsPage ParentPage { get; set; }
    
    private MembersSettings settings;
    private bool isLoading = true;
    private bool isSaving = false;
    private string errorMessage;
    private bool showSuccessMessage = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            settings = await SettingsService.GetSettingsAsync();
            isLoading = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load members settings");
            errorMessage = "Failed to load settings. Please refresh the page.";
            isLoading = false;
        }
    }

    private async Task HandleSave()
    {
        showSuccessMessage = false;
        errorMessage = null;
        isSaving = true;

        try
        {
            var validation = await SettingsService.ValidateAsync(settings);
            if (!validation.IsValid)
            {
                errorMessage = validation.ErrorMessage;
                isSaving = false;
                return;
            }

            await SettingsService.SaveAsync(settings);
            
            Logger.LogInformation("Members settings saved");
            showSuccessMessage = true;
            
            // Clear success message after 3 seconds
            await Task.Delay(3000);
            showSuccessMessage = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save members settings");
            errorMessage = "Failed to save settings. Please try again.";
        }
        finally
        {
            isSaving = false;
        }
    }

    private void OnCancel()
    {
        ParentPage?.OnTabCancel();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Cleanup if needed
    }
}
```

**Settings Tab Styles**:

```css
/* Settings tab container and sections */
.settings-tab {
    padding: 24px;
    max-width: 800px;
}

.settings-section {
    margin-bottom: 32px;
}

.settings-section h3 {
    font-size: 18px;
    font-weight: 600;
    color: #1F2937;
    margin-bottom: 8px;
}

.section-description {
    color: #6B7280;
    font-size: 14px;
    margin-bottom: 16px;
}

.settings-section .form-group {
    margin-bottom: 16px;
}

.settings-section .form-group label {
    display: block;
    margin-bottom: 8px;
    font-weight: 500;
    color: #374151;
}

.settings-section .form-control {
    width: 100%;
    max-width: 400px;
    padding: 8px 12px;
    border: 1px solid #D1D5DB;
    border-radius: 6px;
    font-size: 14px;
    transition: border-color 0.2s;
}

.settings-section .form-control:focus {
    outline: none;
    border-color: #2563EB;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.settings-section .form-text {
    display: block;
    margin-top: 4px;
    color: #6B7280;
    font-size: 12px;
}

/* Checkboxes in settings */
.settings-section .form-group label {
    display: flex;
    align-items: center;
    margin-bottom: 12px;
    cursor: pointer;
}

.settings-section input[type="checkbox"] {
    margin-right: 8px;
    width: 18px;
    height: 18px;
    cursor: pointer;
}

/* Settings actions */
.settings-actions {
    display: flex;
    gap: 12px;
    margin-top: 32px;
    padding-top: 16px;
    border-top: 1px solid #E5E7EB;
}

.settings-actions .btn {
    padding: 10px 16px;
}

/* Application Settings specific styling */
.application-settings {
    background-color: #F9FAFB;
    padding: 16px;
    border-radius: 6px;
    margin-bottom: 16px;
}

.application-settings .form-group {
    display: grid;
    grid-template-columns: 200px 1fr;
    gap: 16px;
    align-items: center;
    margin-bottom: 16px;
}

.application-settings .form-group:last-child {
    margin-bottom: 0;
}

@media (max-width: 640px) {
    .settings-section .form-control {
        max-width: 100%;
    }
    
    .application-settings .form-group {
        grid-template-columns: 1fr;
    }
    
    .settings-tab {
        padding: 16px;
    }
}
```

---

## Do's and Don'ts

### ✅ DO

- Use the spacing scale for all margins and padding
- Write semantic HTML with proper ARIA labels
- Provide visual feedback for all interactions
- Test with keyboard navigation
- Use CSS isolation for component styling
- Keep components focused and single-purpose
- Test on multiple screen sizes

### ❌ DON'T

- Use excessive whitespace; optimize for density
- Add decorative elements that don't serve a purpose
- Rely on color alone for meaning
- Use hardcoded colors; use the palette
- Create massive monolithic components
- Use JavaScript when CSS can do it
- Forget about accessibility

---

## Testing UI Components

### bUnit Testing Example

```csharp
[Fact]
public void Should_DisplayMembersList_When_ComponentRendered()
{
    // Arrange
    var memberService = new Mock<IMemberService>();
    memberService.Setup(s => s.GetActiveMembersAsync())
        .ReturnsAsync(new List<MemberDto>
        {
            new() { Id = 1, Name = "John Doe", Email = "john@example.com" }
        });

    var cut = RenderComponent<MembersComponent>(
        ComponentParameter.CreateParameter("MemberService", memberService.Object)
    );

    // Act - wait for data load
    cut.WaitForAsyncEvents();

    // Assert
    cut.Find("table tbody tr").TextContent.Should().Contain("John Doe");
}

[Fact]
public void Should_ShowErrorAlert_When_LoadingMembersFails()
{
    // Arrange
    var memberService = new Mock<IMemberService>();
    memberService.Setup(s => s.GetActiveMembersAsync())
        .ThrowsAsync(new Exception("API error"));

    var cut = RenderComponent<MembersComponent>(
        ComponentParameter.CreateParameter("MemberService", memberService.Object)
    );

    // Act
    cut.WaitForAsyncEvents();

    // Assert
    cut.Find(".alert-danger").TextContent.Should().Contain("Failed to load members");
}
```

---

## Questions & Support

For questions about the style guide:
- Check the [Contributing Guide](../CONTRIBUTING.md)
- Review component examples in `src/UI/Components/`
- Check existing implemented pages for patterns
- Open an issue with screenshots of the UI element in question
