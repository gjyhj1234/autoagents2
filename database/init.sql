-- Patient management database initialization script

CREATE TABLE IF NOT EXISTS patients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    gender VARCHAR(10) NOT NULL,
    date_of_birth DATE NOT NULL,
    phone VARCHAR(20) NOT NULL,
    address VARCHAR(200),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed data: sample patients
INSERT INTO patients (id, name, gender, date_of_birth, phone, address) VALUES
    (gen_random_uuid(), '张伟', '男', '1985-03-15', '13800138001', '北京市朝阳区建国路88号'),
    (gen_random_uuid(), '李娜', '女', '1990-07-22', '13900139002', '上海市浦东新区陆家嘴路100号'),
    (gen_random_uuid(), '王芳', '女', '1978-11-05', '13700137003', '广州市天河区天河路385号'),
    (gen_random_uuid(), '赵磊', '男', '1995-01-30', '13600136004', '深圳市南山区科技园路18号'),
    (gen_random_uuid(), '陈静', '女', '2000-09-12', '13500135005', NULL);
