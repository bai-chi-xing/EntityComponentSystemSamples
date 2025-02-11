using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    // This Joint allows you to lock one or more of the 6 degrees of freedom of a constrained body.
    // This is achieved by combining the appropriate lower level 'constraint atoms' to form the higher level Joint.
    // In this case Linear and Angular constraint atoms are combined.
    // One use-case for this Joint could be to restrict a 3d simulation to a 2d plane.
    public class LimitDOFJoint : BaseJoint
    {
        public bool3 LockLinearAxes;
        public bool3 LockAngularAxes;

        public PhysicsJoint CreateLimitDOFJoint(RigidTransform offset, bool3 linearLocks, bool3 angularLocks)
        {
            var constraints = new FixedList512Bytes<Constraint>();
            if (math.any(linearLocks))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = linearLocks,
                    Type = ConstraintType.Linear,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = Constraint.DefaultSpringFrequency,
                    SpringDamping = Constraint.DefaultSpringDamping,
                    MaxImpulse = MaxImpulse,
                    EnableImpulseEvents = RaiseImpulseEvents
                });
            }
            if (math.any(angularLocks))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = angularLocks,
                    Type = ConstraintType.Angular,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = Constraint.DefaultSpringFrequency,
                    SpringDamping = Constraint.DefaultSpringDamping,
                    MaxImpulse = MaxImpulse,
                    EnableImpulseEvents = RaiseImpulseEvents
                });
            }

            var joint = new PhysicsJoint
            {
                BodyAFromJoint = BodyFrame.Identity,
                BodyBFromJoint = offset
            };
            joint.SetConstraints(constraints);
            return joint;
        }

        public override void Create(EntityManager entityManager, GameObjectConversionSystem conversionSystem)
        {
            if (!math.any(LockLinearAxes) && !math.any(LockAngularAxes))
                return;

            RigidTransform bFromA = math.mul(math.inverse(worldFromB), worldFromA);
            conversionSystem.World.GetOrCreateSystemManaged<EndJointConversionSystem>().CreateJointEntity(
                this,
                GetConstrainedBodyPair(conversionSystem),
                CreateLimitDOFJoint(bFromA, LockLinearAxes, LockAngularAxes)
            );
        }
    }

    class LimitDOFJointBaker : Baker<LimitDOFJoint>
    {
        public Entity CreateJointEntity(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, PhysicsJoint joint)
        {
            using (var joints = new NativeArray<PhysicsJoint>(1, Allocator.Temp) { [0] = joint })
            using (var jointEntities = new NativeList<Entity>(1, Allocator.Temp))
            {
                CreateJointEntities(worldIndex, constrainedBodyPair, joints, jointEntities);
                return jointEntities[0];
            }
        }

        public void CreateJointEntities(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, NativeArray<PhysicsJoint> joints, NativeList<Entity> newJointEntities = default)
        {
            if (!joints.IsCreated || joints.Length == 0)
                return;

            if (newJointEntities.IsCreated)
                newJointEntities.Clear();
            else
                newJointEntities = new NativeList<Entity>(joints.Length, Allocator.Temp);

            // create all new joints
            var multipleJoints = joints.Length > 1;

            for (var i = 0; i < joints.Length; ++i)
            {
                var jointEntity = CreateAdditionalEntity();
                AddSharedComponent(jointEntity, new PhysicsWorldIndex(worldIndex));

                AddComponent(jointEntity, constrainedBodyPair);
                AddComponent(jointEntity, joints[i]);

                newJointEntities.Add(jointEntity);
            }

            if (multipleJoints)
            {
                // set companion buffers for new joints
                for (var i = 0; i < joints.Length; ++i)
                {
                    var companions = AddBuffer<PhysicsJointCompanion>(newJointEntities[i]);
                    for (var j = 0; j < joints.Length; ++j)
                    {
                        if (i == j)
                            continue;
                        companions.Add(new PhysicsJointCompanion {JointEntity = newJointEntities[j]});
                    }
                }
            }
        }

        protected PhysicsConstrainedBodyPair GetConstrainedBodyPair(LimitDOFJoint authoring)
        {
            return new PhysicsConstrainedBodyPair(
                GetEntity(),
                authoring.ConnectedBody == null ? Entity.Null : GetEntity(authoring.ConnectedBody),
                authoring.EnableCollision
            );
        }

        public uint GetWorldIndex(Component c)
        {
            uint worldIndex = 0;
            var physicsBody = GetComponent<PhysicsBodyAuthoring>(c);
            if (physicsBody != null)
            {
                worldIndex = physicsBody.WorldIndex;
            }
            return worldIndex;
        }

        public override void Bake(LimitDOFJoint authoring)
        {
            if (!math.any(authoring.LockLinearAxes) && !math.any(authoring.LockAngularAxes))
                return;

            RigidTransform bFromA = math.mul(math.inverse(authoring.worldFromB), authoring.worldFromA);
            var worldIndex = GetWorldIndex(authoring);
            CreateJointEntity(
                worldIndex,
                GetConstrainedBodyPair(authoring),
                authoring.CreateLimitDOFJoint(bFromA, authoring.LockLinearAxes, authoring.LockAngularAxes)
            );
        }
    }
}
