import { test, expect, Page } from '@playwright/test';

test.describe('Approval Limits - End to End', () => {
  // Helper function to login as a user
  async function login(page: Page, username: string, password: string): Promise<void> {
    await page.goto('/login');
    await page.fill('input[name="username"]', username);
    await page.fill('input[name="password"]', password);
    await page.click('button[type="submit"]');
    
    // Wait for dashboard or invoices page to load
    await page.waitForSelector('.dashboard, text=Invoices, .invoice-list', { timeout: 5000 });
  }

  test('Manager cannot approve invoice over 50k limit via API', async ({ request }) => {
    // This test verifies that a Manager attempting to approve an invoice 
    // above 50,000 receives a clear error message and the invoice status remains unchanged.
    
    const response = await request.post('/api/Approvals/INV-1/approve', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager'
      }
    });
    
    // Should return 400 Bad Request due to amount limit
    expect(response.status()).toBe(400);
    
    const error = await response.json();
    expect(error.error).toContain('limit');
    expect(error.error).toContain('50,000');
  });

  test('Invoice status unchanged after failed approval attempt', async ({ request }) => {
    // Verify invoice is still pending after failed attempt as Manager
    const response = await request.get('/api/Invoices/INV-1');
    const invoice: any = await response.json();
    
    expect(invoice.Status).toBe('PendingApproval');
  });

  test('Director can approve invoice up to 500k limit', async ({ request }) => {
    // Test that Director can approve an invoice within their limit (400,000)
    const response = await request.post('/api/Approvals/INV-1/approve', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.director'
      }
    });
    
    // Should succeed for amounts <= 500,000
    expect(response.status()).toBe(200);
    
    const approvedInvoice: any = await response.json();
    expect(approvedInvoice.Status).toBe('Approved');
    expect(approvedInvoice.DecidedBy).toBe('demo.director');
  });

  test('Two-approver rule enforced for high-value invoice', async ({ request }) => {
    // Test the two-approver workflow for invoices above 500,000
    
    // First approval as Manager should succeed
    const firstResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager',
        isSecondApprover: false
      }
    });
    
    expect(firstResponse.status()).toBe(200);
    const firstApproval: any = await firstResponse.json();
    expect(firstApproval.Approver1).toBe('demo.manager');
    
    // Second approval as Manager should fail (must be Director)
    const secondResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager',
        isSecondApprover: true
      }
    });
    
    expect(secondResponse.status()).toBe(400);
    const secondError = await secondResponse.json();
    expect(secondError.error).toContain('Director');
    
    // Verify invoice status still pending (no partial approval)
    const invoiceStatus: any = await request.get('/api/Invoices/INV-1').json();
    expect(invoiceStatus.Status).toBe('PendingApproval');
  });

  test('Two different Directors can approve high-value invoice', async ({ request }) => {
    // Test valid two-approver workflow: Manager + different Director
    
    // First approval as Manager
    const firstResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager',
        isSecondApprover: false
      }
    });
    
    expect(firstResponse.status()).toBe(200);
    
    // Second approval as Director should succeed
    const secondResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.director',
        isSecondApprover: true
      }
    });
    
    expect(secondResponse.status()).toBe(200);
    const approvedInvoice: any = await secondResponse.json();
    
    // Verify full approval completed
    expect(approvedInvoice.Status).toBe('Approved');
    expect(approvedInvoice.Approver1).toBe('demo.manager');
    expect(approvedInvoice.Approver2).toBe('demo.director');
  });

  test('Same user cannot be both approvers on high-value invoice', async ({ request }) => {
    // Test that the same Director cannot approve twice
    
    // First approval as Director
    const firstResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.director',
        isSecondApprover: false
      }
    });
    
    expect(firstResponse.status()).toBe(200);
    
    // Second approval by same Director should fail
    const secondResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.director',
        isSecondApprover: true
      }
    });
    
    expect(secondResponse.status()).toBe(400);
    const secondError = await secondResponse.json();
    expect(secondError.error).toContain('Same user');
    expect(secondError.error).toContain('both approvers');
  });

  test('Manager cannot approve invoice over 50k via API with clear error', async ({ request }) => {
    // Test that error message is clear and actionable
    
    const response = await request.post('/api/Approvals/INV-1/approve', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager'
      }
    });
    
    expect(response.status()).toBe(400);
    
    const error: any = await response.json();
    // Error should mention the limit amount
    expect(error.error).toMatch(/exceeds.*limit/);
  });

  test('Invoice remains in pending state throughout failed two-approver workflow', async ({ request }) => {
    // Verify that even after attempting first approval and failing second, 
    // invoice status never changes from PendingApproval
    
    const initialStatus: any = await request.get('/api/Invoices/INV-1').json();
    expect(initialStatus.Status).toBe('PendingApproval');
    
    // Attempt first approval as Manager
    const firstResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager',
        isSecondApprover: false
      }
    });
    
    expect(firstResponse.status()).toBe(200);
    
    // Attempt second approval as Manager (should fail)
    const secondResponse = await request.post('/api/Approvals/INV-1/approve/two', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.manager',
        isSecondApprover: true
      }
    });
    
    expect(secondResponse.status()).toBe(400);
    
    // Verify status still pending - no partial state changes
    const finalStatus: any = await request.get('/api/Invoices/INV-1').json();
    expect(finalStatus.Status).toBe('PendingApproval');
  });

  test('Director cannot approve invoice over 500k as single approver', async ({ request }) => {
    // Test that Director also has limit enforcement
    
    const response = await request.post('/api/Approvals/INV-1/approve', {
      data: {
        invoiceId: 'INV-1',
        approver: 'demo.director'
      }
    });
    
    expect(response.status()).toBe(400);
    const error: any = await response.json();
    expect(error.error).toContain('limit');
  });
});
