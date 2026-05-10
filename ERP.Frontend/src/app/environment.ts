export const environment = {
  production: false,
  docker: false,

  apiUrl: 'http://localhost:5000',
  
  routes: {
    auth:        '/auth',
    roles:       '/auth/roles',
    controles:   '/auth/controles',
    privileges:  '/auth/privileges',
    articles:    '/articles',
    clients:     '/clients',
    stock:       '/stock',
    fournisseurs:'/fournisseurs',
    invoices:    '/invoices',
    tenants:   '/tenants',
  },
} as const;