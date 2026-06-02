import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubscriptionExpiry } from './subscription-expiry';

describe('SubscriptionExpiry', () => {
  let component: SubscriptionExpiry;
  let fixture: ComponentFixture<SubscriptionExpiry>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubscriptionExpiry]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubscriptionExpiry);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
